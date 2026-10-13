using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VisualNovel
{
    /// <summary>
    /// 배경 / 인물 / 닉네임 / 대사를 외부(txt·JSON 파일, 문자열, 코드 API) 또는 인스펙터 값으로 받아 미연시 화면처럼 출력한다.
    /// </summary>
    public class VNPlayer : MonoBehaviour
    {
        public enum ScriptSource { Inspector, File, Text }
        public enum Slot { Left = 0, Center = 1, Right = 2 }

        [Header("데이터 소스")]
        [Tooltip("Inspector: 아래 Script 값 사용 / File: 대본 파일(txt 또는 json) / Text: 아래 Script Text 사용")]
        public ScriptSource source = ScriptSource.File;

        [Tooltip("빌드 시 exe 옆의 이 폴더에서 대본/이미지를 읽음 (없으면 StreamingAssets/VN 을 복사해 자동 생성)")]
        public string contentFolder = "VN";

        [Tooltip("빌드에서 exe 옆 외부 폴더 사용 (끄면 내장 StreamingAssets/VN 만 사용)")]
        public bool useExternalFolder = true;

        [Tooltip("대본 파일 (.txt 또는 .json). 콘텐츠 폴더 기준 상대경로, 절대경로, http URL. 실행 인자 -vnscript <경로> 로 덮어쓸 수 있음")]
        [FormerlySerializedAs("jsonPath")]
        public string scriptPath = "script.txt";

        [Tooltip("처음부터 표시할 기본 배경. 대사 파일의 [배경] 명령이 나오면 그 이미지로 바뀜")]
        public string defaultBackground = "";

        [Tooltip("txt 대본 문법 또는 JSON 문자열")]
        [TextArea(4, 20)]
        [FormerlySerializedAs("jsonText")]
        public string scriptText;

        [Tooltip("인스펙터에서 직접 작성하는 스크립트")]
        public VNScript script = new VNScript();

        [Header("재생 설정")]
        public bool playOnStart = true;
        [Tooltip("초당 출력 글자 수 (0 이면 즉시 출력)")]
        public float charsPerSecond = 40f;
        [Tooltip("말하지 않는 인물을 어둡게 표시")]
        public bool dimInactiveCharacters = true;
        [Tooltip("콘텐츠 폴더의 파일(JSON·이미지)이 바뀌면 자동으로 다시 불러옴 (에디터/PC)")]
        public bool watchFileChanges = true;
        [Tooltip("마지막 대사 이후 처음부터 반복")]
        public bool loop = false;

        [Header("폰트 (비우면 OS 한글 폰트 자동 사용)")]
        public Font font;

        [Header("UI 참조 (비어 있으면 자동 생성)")]
        public Image backgroundImage;
        public AspectRatioFitter backgroundFitter;
        public Image[] characterSlots = new Image[3];
        public GameObject dialogueBox;
        public GameObject namePlate;
        public Text nameText;
        public Text dialogueText;
        public GameObject nextIndicator;

        [Header("이벤트")]
        public UnityEvent<int> onLineShown;
        public UnityEvent onFinished;

        [Header("인물 표시")]
        [Tooltip("모든 인물에 곱해지는 크기 배율. 전신/초상화·캐릭터별 크기·높이는 VNCharacterDef 의 portrait / scale / offsetY")]
        [Range(0.5f, 1.5f)] public float characterScale = 1f;

        /// <summary>false 면 클릭/키 입력으로 대사가 넘어가지 않음 (일시정지 메뉴 등)</summary>
        [NonSerialized] public bool inputEnabled = true;

        /// <summary>설정 화면에서 지정한 캐릭터 (대본의 @캐릭터 보다 우선)</summary>
        public readonly List<VNCharacterDef> presetCharacters = new List<VNCharacterDef>();
        /// <summary>캐릭터 id → 기본 위치</summary>
        public readonly Dictionary<string, string> presetPositions = new Dictionary<string, string>();
        /// <summary>대사에서 쓰는 별칭 → 캐릭터 id (예: "A" → "유나")</summary>
        public readonly Dictionary<string, string> speakerAliases = new Dictionary<string, string>();

        public int CurrentIndex { get; private set; } = -1;
        public bool HasError => loadError;
        public bool IsTyping { get; private set; }
        public VNScript Script => script;

        readonly Dictionary<string, string> scriptVars = new Dictionary<string, string>();
        readonly Dictionary<string, string> overrideVars = new Dictionary<string, string>();
        readonly Dictionary<string, VNCharacterDef> charDefs = new Dictionary<string, VNCharacterDef>();
        Slot? speakingSlot;
        string fullText = "";
        Coroutine typing, showing;
        long folderFingerprint;
        bool reloading;
        float nextWatchCheck;

        // ───────────────────────── 수명주기 ─────────────────────────

        void Awake()
        {
            if (backgroundImage == null || dialogueText == null) VNUIBuilder.Build(this);
            if (font == null)
            {
                var osFont = VNUIBuilder.DefaultFont();
                foreach (var t in GetComponentsInChildren<Text>(true)) t.font = osFont;
            }
            ApplyCommandLine();
            ApplyCommandLineVars();
            VNContent.Resolve(contentFolder, useExternalFolder);
            Debug.Log($"[VN] 콘텐츠 폴더: {VNContent.Root}");
        }

        IEnumerator Start()
        {
            try { folderFingerprint = VNContent.Fingerprint(); } catch { }
            if (!playOnStart) yield break;
            loadError = false;
            yield return LoadFromSource();
            if (!loadError) Play();
        }

        bool loadError;

        /// <summary>로드 오류를 게임 화면에 표시 (외부 파일 수정 중 실수 확인용)</summary>
        void ShowError(string message)
        {
            loadError = true;
            Debug.LogError("[VN] " + message);
            if (showing != null) { StopCoroutine(showing); showing = null; }
            if (typing != null) { StopCoroutine(typing); typing = null; }
            IsTyping = false;
            SetDialogueBoxVisible(true);
            if (namePlate) namePlate.SetActive(true);
            if (nameText) { nameText.text = "오류"; nameText.color = new Color(1f, 0.5f, 0.5f); }
            if (dialogueText) dialogueText.text = message + "\n<size=28>파일을 고쳐서 저장하면 자동으로 다시 불러옵니다.</size>";
            if (nextIndicator) nextIndicator.SetActive(false);
        }

        void Update()
        {
            if (nextIndicator != null && nextIndicator.activeSelf)
            {
                float a = 0.4f + 0.6f * Mathf.PingPong(Time.time * 1.5f, 1f);
                var baseColor = dialogueText ? dialogueText.color : Color.white;
                foreach (var g in nextIndicator.GetComponentsInChildren<Graphic>())
                    g.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            }

            if (inputEnabled && AdvancePressed()) Next();
            if (ReloadPressed()) StartCoroutine(ReloadKeepPosition());

            if (watchFileChanges && !reloading && Time.unscaledTime >= nextWatchCheck)
            {
                nextWatchCheck = Time.unscaledTime + 1f;
                long fp = 0;
                try { fp = VNContent.Fingerprint(); } catch { }
                if (fp != 0 && folderFingerprint != 0 && fp != folderFingerprint) StartCoroutine(ReloadKeepPosition());
                if (fp != 0) folderFingerprint = fp;
            }
        }

        // ───────────────────────── 로드 ─────────────────────────

        /// <summary>현재 source 설정에 따라 스크립트를 불러온다.</summary>
        public IEnumerator LoadFromSource()
        {
            switch (source)
            {
                case ScriptSource.File: yield return LoadScriptFile(scriptPath); break;
                case ScriptSource.Text: LoadScriptText(scriptText); break;
                default: SetScript(script); break;
            }
        }

        /// <summary>대본 파일(txt/json, 상대/절대 경로, URL)을 불러온다.</summary>
        public IEnumerator LoadScriptFile(string path)
        {
            string url;
            if (path.Contains("://")) url = path;
            else if (Path.IsPathRooted(path)) url = new Uri(path).AbsoluteUri;
            else
            {
                var full = Path.Combine(VNContent.Root ?? VNContent.BuiltinRoot, path);
                url = full.Contains("://") ? full : new Uri(full).AbsoluteUri; // Android 는 이미 URL
            }

            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    ShowError($"대본 파일을 불러오지 못했습니다.\n{Uri.UnescapeDataString(url)}\n({req.error})");
                    yield break;
                }
                LoadScriptText(VNTextParser.DecodeText(req.downloadHandler.data), path);
            }
        }

        /// <summary>txt 대본 또는 JSON 문자열로 스크립트를 교체한다. (내용이 { 로 시작하거나 .json 파일이면 JSON)</summary>
        public void LoadScriptText(string text, string fileName = null)
        {
            if (string.IsNullOrWhiteSpace(text)) { ShowError("대본이 비어 있습니다."); return; }
            bool isJson = text.TrimStart().StartsWith("{") ||
                          (fileName != null && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (isJson) { LoadJson(text); return; }

            var warnings = new List<string>();
            var parsed = VNTextParser.Parse(text, warnings, presetCharacters, presetPositions, speakerAliases);
            if (parsed.lines.Count == 0) { ShowError("대본에 대사가 없습니다." + (warnings.Count > 0 ? "\n" + warnings[0] : "")); return; }
            SetScript(parsed);
        }

        /// <summary>JSON 문자열로 스크립트를 교체한다.</summary>
        public void LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) { Debug.LogWarning("[VN] JSON 이 비어 있습니다."); return; }
            try { SetScript(JsonUtility.FromJson<VNScript>(json)); }
            catch (Exception e) { ShowError($"JSON 형식 오류 (쉼표, 따옴표, 괄호를 확인하세요)\n{e.Message}"); }
        }

        /// <summary>스크립트 객체로 교체한다.</summary>
        public void SetScript(VNScript s)
        {
            script = s ?? new VNScript();
            charDefs.Clear();
            foreach (var c in script.characters)
                if (c != null && !string.IsNullOrEmpty(c.id)) charDefs[c.id] = c;
            foreach (var c in presetCharacters)
                if (c != null && !string.IsNullOrEmpty(c.id)) charDefs[c.id] = c;
            scriptVars.Clear();
            foreach (var v in script.variables)
                if (v != null && !string.IsNullOrEmpty(v.key)) scriptVars[v.key] = v.value;
        }

        IEnumerator ReloadKeepPosition()
        {
            if (reloading) yield break;
            reloading = true;
            // 파일 저장이 끝날 때까지 잠시 대기
            yield return new WaitForSecondsRealtime(0.2f);
            int idx = Mathf.Max(CurrentIndex, 0);
            VNImageLoader.ClearCache();
            loadError = false;
            yield return LoadFromSource();
            try { folderFingerprint = VNContent.Fingerprint(); } catch { }
            reloading = false;
            if (loadError) yield break;
            Debug.Log("[VN] 콘텐츠 다시 불러옴");
            // 이전 대사들의 배경/인물 상태를 재현한 뒤 해당 대사 표시
            JumpTo(idx);
        }

        // ───────────────────────── 재생 ─────────────────────────

        public void Play() { ResetStage(); JumpTo(0); }

        /// <summary>다음으로 진행. 타이핑 중이면 즉시 완성.</summary>
        public void Next()
        {
            if (IsTyping) { CompleteTyping(); return; }
            if (showing != null) return;
            if (script == null || script.lines.Count == 0) return;
            if (CurrentIndex + 1 >= script.lines.Count)
            {
                onFinished?.Invoke();
                if (loop) Play();
                return;
            }
            ShowIndex(CurrentIndex + 1);
        }

        /// <summary>index 번째 대사로 이동 (앞선 배경/인물 상태 누적 적용).</summary>
        public void JumpTo(int index)
        {
            if (script == null || script.lines.Count == 0) return;
            index = Mathf.Clamp(index, 0, script.lines.Count - 1);
            if (showing != null) StopCoroutine(showing);
            showing = StartCoroutine(JumpRoutine(index));
        }

        IEnumerator JumpRoutine(int index)
        {
            ResetStage();
            if (!string.IsNullOrWhiteSpace(defaultBackground))
                yield return VNImageLoader.Load(defaultBackground, ApplyBackground);
            for (int i = 0; i < index; i++) yield return ApplyStage(script.lines[i]);
            showing = null;
            ShowIndex(index);
        }

        void ShowIndex(int index)
        {
            if (showing != null) StopCoroutine(showing);
            showing = StartCoroutine(ShowRoutine(index));
        }

        IEnumerator ShowRoutine(int index)
        {
            CurrentIndex = index;
            var line = script.lines[index];
            yield return ApplyStage(line);
            ApplyNameAndText(line);
            showing = null;
            onLineShown?.Invoke(index);
        }

        /// <summary>대사 한 줄을 스크립트와 무관하게 즉시 표시한다 (외부 코드에서 직접 호출용).</summary>
        public void ShowLine(VNLine line)
        {
            if (showing != null) StopCoroutine(showing);
            showing = StartCoroutine(ShowSingle(line));
        }

        IEnumerator ShowSingle(VNLine line)
        {
            yield return ApplyStage(line);
            ApplyNameAndText(line);
            showing = null;
        }

        // ───────────────────────── 개별 설정 API ─────────────────────────

        public void SetBackground(Sprite sprite) => ApplyBackground(sprite);
        public void SetBackground(string path) => StartCoroutine(LoadThen(path, ApplyBackground));

        public void SetCharacter(Slot slot, Sprite sprite) => ApplyCharacter(slot, sprite);
        public void SetCharacter(Slot slot, string path) => StartCoroutine(LoadThen(path, s => ApplyCharacter(slot, s)));
        public void ClearCharacter(Slot slot) => ApplyCharacter(slot, null);
        public void ClearAllCharacters() { for (int i = 0; i < characterSlots.Length; i++) ApplyCharacter((Slot)i, null); }

        /// <summary>이름표 닉네임 설정. null/빈 문자열이면 이름표 숨김.</summary>
        public void SetNickname(string nickname, Color? color = null)
        {
            bool show = !string.IsNullOrEmpty(nickname);
            if (namePlate) namePlate.SetActive(show);
            if (nameText)
            {
                nameText.text = show ? ReplaceVars(nickname) : "";
                nameText.color = color ?? Color.white;
            }
        }

        /// <summary>대사 텍스트 설정 (타이핑 효과 적용).</summary>
        public void SetDialogue(string text) => StartTyping(ReplaceVars(text ?? ""));

        /// <summary>닉네임 + 대사를 한 번에.</summary>
        public void Say(string nickname, string text) { SetNickname(nickname); SetDialogue(text); }

        /// <summary>{키} 치환 변수 설정 (스크립트 값보다 우선). 예: SetVariable("player", "모아")</summary>
        public void SetVariable(string key, string value) => overrideVars[key] = value;

        public void SetDialogueBoxVisible(bool visible) { if (dialogueBox) dialogueBox.SetActive(visible); }

        // ───────────────────────── 내부 처리 ─────────────────────────

        IEnumerator ApplyStage(VNLine line)
        {
            if (line.clearCharacters) ClearAllCharacters();
            if (!string.IsNullOrEmpty(line.clearSlots))
                foreach (var s in line.clearSlots.Split(',')) ApplyCharacter(ParseSlot(s), null);

            // 배경
            if (line.backgroundSprite != null) ApplyBackground(line.backgroundSprite);
            else if (IsNone(line.background)) ApplyBackground(null);
            else if (!string.IsNullOrWhiteSpace(line.background))
                yield return VNImageLoader.Load(line.background, ApplyBackground);

            // 인물
            charDefs.TryGetValue(line.character ?? "", out var def);
            var slot = ParseSlot(line.position);
            bool hasSpeakerImage = false;

            // def 를 함께 넘겨 캐릭터별 전신/초상화 설정을 적용 (표정 이미지도 같은 방식으로 자름)
            if (line.sprite != null) { ApplyCharacter(slot, line.sprite, def); hasSpeakerImage = true; }
            else if (IsNone(line.image)) ApplyCharacter(slot, null);
            else if (!string.IsNullOrWhiteSpace(line.image))
            {
                yield return VNImageLoader.Load(line.image, s => ApplyCharacter(slot, s, def));
                hasSpeakerImage = true;
            }
            else if (def != null)
            {
                if (def.sprite != null) ApplyCharacter(slot, def.sprite, def);
                else if (!string.IsNullOrWhiteSpace(def.image))
                    yield return VNImageLoader.Load(def.image, s => ApplyCharacter(slot, s, def));
                hasSpeakerImage = true;
            }

            speakingSlot = hasSpeakerImage ? slot : (Slot?)null;
            UpdateDimming();
        }

        void ApplyNameAndText(VNLine line)
        {
            charDefs.TryGetValue(line.character ?? "", out var def);
            string nick = IsNone(line.nickname) ? null
                : !string.IsNullOrEmpty(line.nickname) ? line.nickname
                : def?.nickname;
            Color? col = null;
            if (def != null && ColorUtility.TryParseHtmlString(def.nameColor, out var c)) col = c;
            SetNickname(nick, col);
            SetDialogue(line.text);
        }

        void ApplyBackground(Sprite s)
        {
            if (!backgroundImage) return;
            backgroundImage.sprite = s;
            backgroundImage.enabled = s != null;
            if (s != null && backgroundFitter) backgroundFitter.aspectRatio = s.rect.width / s.rect.height;
        }

        readonly Sprite[] slotSources = new Sprite[3];
        readonly VNCharacterDef[] slotDefs = new VNCharacterDef[3];
        readonly Dictionary<Sprite, (float aspect, float focus, Sprite cropped)> cropCache =
            new Dictionary<Sprite, (float, float, Sprite)>();

        void ApplyCharacter(Slot slot, Sprite s, VNCharacterDef def = null)
        {
            int i = (int)slot;
            if (characterSlots == null || i >= characterSlots.Length || !characterSlots[i]) return;
            if (i < slotSources.Length) { slotSources[i] = s; slotDefs[i] = s != null ? def : null; }
            bool portrait = s != null && def != null && def.portrait;
            if (portrait) s = CropCached(s, def.portraitAspect > 0f ? def.portraitAspect : 4f / 3f, def.portraitFocus);
            if (i < VNLayout.SlotX.Length)
                VNLayout.ApplySlot(characterSlots[i].rectTransform, i, portrait,
                    (def != null ? def.scale : 1f) * characterScale, def != null ? def.offsetY : 0f);
            var img = characterSlots[i];
            img.sprite = s;
            img.enabled = s != null;
            if (s != null && img.TryGetComponent<AspectRatioFitter>(out var fit))
                fit.aspectRatio = s.rect.width / s.rect.height;
        }

        /// <summary>같은 이미지·같은 설정이면 이전에 자른 스프라이트를 재사용</summary>
        Sprite CropCached(Sprite s, float aspect, float focus)
        {
            if (cropCache.TryGetValue(s, out var c) && c.cropped != null &&
                Mathf.Approximately(c.aspect, aspect) && Mathf.Approximately(c.focus, focus))
                return c.cropped;
            if (c.cropped != null) Destroy(c.cropped);
            var cropped = CropSprite(s, aspect, focus);
            cropCache[s] = (aspect, focus, cropped);
            return cropped;
        }

        /// <summary>
        /// 스프라이트를 aspect(가로/세로) 비율로 자른다.
        /// 세로가 남으면 focus(0 = 맨 위, 1 = 맨 아래) 위치를, 가로가 남으면 가운데를 남긴다.
        /// </summary>
        public static Sprite CropSprite(Sprite s, float aspect, float focus)
        {
            if (s == null || aspect <= 0f) return s;
            Rect r = s.rect;
            Rect c;
            float targetH = r.width / aspect;
            if (targetH <= r.height)
            {
                // 세로가 남으면 위(focus=0)~아래(focus=1) 중 원하는 부분을 자름. 텍스처 y 는 아래가 0
                float y = r.y + (r.height - targetH) * (1f - focus);
                c = new Rect(r.x, y, r.width, targetH);
            }
            else
            {
                // 가로가 남으면 가운데를 자름
                float targetW = r.height * aspect;
                c = new Rect(r.x + (r.width - targetW) * 0.5f, r.y, targetW, r.height);
            }
            var cropped = Sprite.Create(s.texture, c, new Vector2(0.5f, 0f), s.pixelsPerUnit);
            cropped.name = s.name + "_portrait";
            return cropped;
        }

        /// <summary>인물 크기·캐릭터별 표시 방식 변경을 현재 화면에 다시 반영</summary>
        public void ApplyDisplaySettings()
        {
            for (int i = 0; i < characterSlots.Length && i < slotSources.Length; i++)
                if (characterSlots[i]) ApplyCharacter((Slot)i, slotSources[i], slotDefs[i]);
        }

        // ───────────────────────── UI (대사 박스 / 네임 박스 / 폰트) ─────────────────────────

        bool skinDefaultsSaved;
        Sprite defBoxSprite, defPlateSprite;
        Color defBoxColor, defPlateColor, defTextColor;
        Image.Type defBoxType, defPlateType;
        Font defFont;

        /// <summary>UI 설정 적용. null 이면 기본 UI 로 되돌림</summary>
        public void ApplySkin(VNSkin skin)
        {
            var box = dialogueBox ? dialogueBox.GetComponent<Image>() : null;
            var plate = namePlate ? namePlate.GetComponent<Image>() : null;
            if (!skinDefaultsSaved)
            {
                skinDefaultsSaved = true;
                if (box) { defBoxSprite = box.sprite; defBoxColor = box.color; defBoxType = box.type; }
                if (plate) { defPlateSprite = plate.sprite; defPlateColor = plate.color; defPlateType = plate.type; }
                if (dialogueText) { defTextColor = dialogueText.color; defFont = dialogueText.font; }
            }

            SetSkinImage(box, skin?.dialogueBox, skin?.dialogueBoxBorder ?? 0, defBoxSprite, defBoxColor, defBoxType);
            SetSkinImage(plate, skin?.namePlate, skin?.namePlateBorder ?? 0, defPlateSprite, defPlateColor, defPlateType);
            if (dialogueText)
            {
                dialogueText.font = skin?.font != null ? skin.font : defFont;
                dialogueText.color = skin?.textColor ?? defTextColor;
            }
        }

        static void SetSkinImage(Image img, Sprite sprite, int border, Sprite defSprite, Color defColor, Image.Type defType)
        {
            if (!img) return;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.type = border > 0 ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = 1f;
            }
            else
            {
                img.sprite = defSprite;
                img.color = defColor;
                img.type = defType;
            }
        }

        /// <summary>설정 화면 값 적용</summary>
        public void ApplySettings(VNSettings st)
        {
            charsPerSecond = st.CharsPerSecond;

            presetCharacters.Clear();
            presetPositions.Clear();
            speakerAliases.Clear();
            AddPreset("A", st.charAName, st.charAImage, "left", "#FFB3D9", st, 0);
            AddPreset("B", st.charBName, st.charBImage, "right", "#A8D8FF", st, 1);

            source = ScriptSource.File;
            scriptPath = st.ResolveScriptPath() ?? "";
            defaultBackground = st.backgroundImage?.Trim() ?? "";
            ApplyDisplaySettings();
        }

        void AddPreset(string alias, string name, string image, string pos, string color, VNSettings st, int idx)
        {
            if (string.IsNullOrWhiteSpace(name)) name = alias;
            name = name.Trim();
            presetCharacters.Add(new VNCharacterDef
            {
                id = name, nickname = name, image = image?.Trim(), nameColor = color,
                portrait = st.GetMode(idx) == VNSettings.DisplayMode.Portrait,
                portraitAspect = VNSettings.AspectOf(st.GetRatio(idx)),
                portraitFocus = st.GetFocus(idx),
                scale = st.GetScale(idx),
                offsetY = st.GetOffset(idx),
            });
            presetPositions[name] = pos;
            speakerAliases[alias] = name;
        }

        /// <summary>대본을 불러와 처음부터 재생</summary>
        public IEnumerator Begin()
        {
            loadError = false;
            CurrentIndex = -1;
            ResetStage();
            if (source == ScriptSource.File && string.IsNullOrEmpty(scriptPath))
            {
                ShowError("대사 파일이 없습니다.\nVN/대사 폴더에 txt 파일을 넣거나 설정 화면에서 파일을 첨부하세요.");
                yield break;
            }
            VNImageLoader.ClearCache();
            yield return LoadFromSource();
            try { folderFingerprint = VNContent.Fingerprint(); } catch { }
            if (!loadError) Play();
        }

        void UpdateDimming()
        {
            for (int i = 0; i < characterSlots.Length; i++)
            {
                if (!characterSlots[i]) continue;
                bool active = !dimInactiveCharacters || speakingSlot == null || (int)speakingSlot.Value == i;
                characterSlots[i].color = active ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            }
        }

        void ResetStage()
        {
            ApplyBackground(null);
            ClearAllCharacters();
            speakingSlot = null;
            SetNickname(null);
            if (dialogueText) dialogueText.text = "";
            if (nextIndicator) nextIndicator.SetActive(false);
        }

        IEnumerator LoadThen(string path, Action<Sprite> apply)
        {
            if (IsNone(path)) { apply(null); yield break; }
            yield return VNImageLoader.Load(path, apply);
        }

        // ───────────────────────── 타이핑 ─────────────────────────

        void StartTyping(string text)
        {
            fullText = text;
            if (typing != null) StopCoroutine(typing);
            if (nextIndicator) nextIndicator.SetActive(false);
            if (charsPerSecond <= 0f) { CompleteTyping(); return; }
            typing = StartCoroutine(TypeRoutine());
        }

        IEnumerator TypeRoutine()
        {
            IsTyping = true;
            var sb = new StringBuilder();
            float delay = 1f / charsPerSecond;
            for (int i = 0; i < fullText.Length; i++)
            {
                // 리치텍스트 태그(<color=..> 등)는 한 번에 출력
                if (fullText[i] == '<')
                {
                    int end = fullText.IndexOf('>', i);
                    if (end > i) { sb.Append(fullText, i, end - i + 1); i = end; continue; }
                }
                sb.Append(fullText[i]);
                dialogueText.text = sb.ToString();
                yield return new WaitForSeconds(delay);
            }
            CompleteTyping();
        }

        void CompleteTyping()
        {
            if (typing != null) { StopCoroutine(typing); typing = null; }
            IsTyping = false;
            if (dialogueText) dialogueText.text = fullText;
            if (nextIndicator) nextIndicator.SetActive(true);
        }

        // ───────────────────────── 유틸 ─────────────────────────

        string ReplaceVars(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('{') < 0) return s;
            foreach (var kv in overrideVars) s = s.Replace("{" + kv.Key + "}", kv.Value);
            foreach (var kv in scriptVars) s = s.Replace("{" + kv.Key + "}", kv.Value);
            return s;
        }

        static bool IsNone(string s) => s != null && s.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);

        static Slot ParseSlot(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "left": case "l": case "왼쪽": return Slot.Left;
                case "right": case "r": case "오른쪽": return Slot.Right;
                default: return Slot.Center;
            }
        }

        void ApplyCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-vnscript") { source = ScriptSource.File; scriptPath = args[i + 1]; }
            }
        }

        void ApplyCommandLineVars()
        {
            // -vnvar key=value (여러 번 가능)
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-vnvar") continue;
                var kv = args[i + 1].Split(new[] { '=' }, 2);
                if (kv.Length == 2) overrideVars[kv[0]] = kv[1];
            }
        }

        bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return !IsPointerOverButton(mouse.position.ReadValue());
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return !IsPointerOverButton(touch.primaryTouch.position.ReadValue());
            return false;
#else
            if (Input.GetMouseButtonDown(0)) return !IsPointerOverButton(Input.mousePosition);
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
        }

        static readonly List<RaycastResult> raycastHits = new List<RaycastResult>();

        /// <summary>클릭 위치에 버튼 등 UI 조작 요소가 있으면 대사를 넘기지 않음 (종료 버튼 클릭 등)</summary>
        static bool IsPointerOverButton(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            raycastHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, raycastHits);
            foreach (var h in raycastHits)
                if (h.gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }

        bool ReloadPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F5);
#endif
        }
    }
}
