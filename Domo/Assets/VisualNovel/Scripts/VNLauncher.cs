using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using static VisualNovel.VNUIKit;

namespace VisualNovel
{
    /// <summary>
    /// 실행 시 먼저 나오는 설정 화면 + 게임 중 종료 버튼 / ESC 메뉴.
    ///  탭: 캐릭터 설정 / 대사 · 배경음 / 화면 설정
    ///  "출력하기" 를 누르면 VNPlayer 로 대사를 재생한다.
    /// </summary>
    public class VNLauncher : MonoBehaviour
    {
        public VNPlayer player;
        [Header("제목")]
        public string titleMain = "VN";
        public string titleAccent = "Player";
        public string versionText = "v1.0";

        VNSettings st;
        Canvas menuCanvas, hudCanvas, pauseCanvas;
        GameObject[] pages;
        Pill[] tabs;
        Text footerNote;

        // 캐릭터 탭
        readonly InputField[] nameFields = new InputField[2];
        readonly InputField[] imageFields = new InputField[2];
        readonly Image[] thumbs = new Image[2];
        readonly Text[] thumbEmpty = new Text[2];
        readonly Text[] imageStatus = new Text[2];

        // 대사 · 배경음 탭
        Selector scriptSel, bgmSel;
        InputField attachField;
        Text scriptStatus, bgmStatus;
        Pill previewBgmButton;
        Slider volumeSlider;
        bool previewingBgm;

        // 화면 탭
        readonly GameObject[] portraitOpts = new GameObject[2];
        readonly Sprite[] cropSources = new Sprite[2];

        AudioSource bgm;
        string bgmLoadedPath;
        System.DateTime bgmLoadedTime;
        bool inGame, paused;

        const string DefaultNote = "변경한 설정은 바로 적용되고 자동으로 저장됩니다.";

        // ───────────────────────── 수명주기 ─────────────────────────

        void Awake()
        {
            if (!player) player = FindAnyObjectByType<VNPlayer>();
            player.playOnStart = false;
            VNContent.Resolve(player.contentFolder, player.useExternalFolder);
            font = player.font != null ? player.font : VNUIBuilder.DefaultFont();
            st = VNSettings.Load();

            EnsureEventSystem();
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.loop = true;
            bgm.playOnAwake = false;

            BuildMenu();
            BuildHud();
            BuildPause();
            player.onFinished.AddListener(OnScriptFinished);
            ApplyWindowMode();
        }

        void Start() => ShowMenu();

        void Update()
        {
            if (inGame && EscapePressed())
            {
                if (paused) Resume();
                else Pause();
            }
        }

        void OnApplicationFocus(bool focus)
        {
            // 탐색기에서 파일을 넣고 돌아오면 목록 갱신
            if (focus && menuCanvas != null && menuCanvas.enabled) RefreshAll();
        }

        // ───────────────────────── 화면 전환 ─────────────────────────

        public void ShowMenu()
        {
            inGame = false;
            paused = false;
            player.gameObject.SetActive(false);
            hudCanvas.enabled = false;
            pauseCanvas.enabled = false;
            menuCanvas.enabled = true;
            StopBgm();
            SetNote(DefaultNote, false);
            RefreshAll();
        }

        public void StartGame()
        {
            StopPreview();
            if (st.ResolveScriptPath() == null)
            {
                SelectTab(1);
                SetNote("대사 파일이 없습니다. VN/대사 폴더에 txt 파일을 넣거나 파일을 첨부하세요.", true);
                return;
            }
            st.Save();

            menuCanvas.enabled = false;
            hudCanvas.enabled = true;
            pauseCanvas.enabled = false;
            inGame = true;
            paused = false;

            player.gameObject.SetActive(true);
            player.inputEnabled = true;
            player.ApplySettings(st);
            player.StartCoroutine(player.Begin());
            var bgmPath = st.ResolveBgmPath();
            if (bgmPath != null) StartCoroutine(PlayBgm(bgmPath, true));
        }

        void Pause()
        {
            paused = true;
            pauseCanvas.enabled = true;
            player.inputEnabled = false;
            if (bgm.isPlaying) bgm.Pause();
        }

        void Resume()
        {
            paused = false;
            pauseCanvas.enabled = false;
            player.inputEnabled = true;
            if (bgm.clip != null && st.bgmEnabled) bgm.UnPause();
        }

        void OnScriptFinished()
        {
            if (inGame && !paused) ShowMenu();
        }

        public void Quit()
        {
            st.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────────────────────── 설정 화면 ─────────────────────────

        void BuildMenu()
        {
            menuCanvas = MakeCanvas("Menu Canvas", 10);
            var root = (RectTransform)menuCanvas.transform;

            var bg = NewRect("Background", root);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = Bg;

            var bar = NewRect("TopBar", root);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1); bar.sizeDelta = new Vector2(0, 8);
            bar.gameObject.AddComponent<Image>().color = Teal;

            var panel = NewRect("Panel", root);
            panel.anchorMin = new Vector2(0.5f, 0); panel.anchorMax = new Vector2(0.5f, 1);
            panel.sizeDelta = new Vector2(1200, 0);
            panel.offsetMin = new Vector2(panel.offsetMin.x, 34);
            panel.offsetMax = new Vector2(panel.offsetMax.x, -40);
            VLayout(panel, 24);

            // 머리말
            var header = Row(panel, 20, 80);
            var title = Label(header, $"<b>{titleMain}</b> <color=#14B8A6><b>{titleAccent}</b></color>   <size=28><color=#6B7280>설정</color></size>", 50, Ink);
            Size(title, flexW: 1);
            PillButton(header, "콘텐츠 폴더 열기", PillStyle.Outline, () => VNContent.OpenFolder(null), -1, 54, 22);
            var ver = Label(header, versionText, 22, Sub, false, TextAnchor.MiddleRight);
            Size(ver, w: 90);

            // 탭
            var tabRow = Row(panel, 16, 66);
            string[] tabNames = { "캐릭터 설정", "대사 · 배경음", "화면 설정" };
            tabs = new Pill[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                int idx = i;
                tabs[i] = PillButton(tabRow, tabNames[i], PillStyle.Outline, () => SelectTab(idx), 250, 66, 27);
            }

            // 내용
            var scroll = NewRect("Scroll", panel);
            Size(scroll, flexH: 1);
            var content = ScrollArea(scroll);
            pages = new GameObject[3];
            pages[0] = BuildCharacterPage(content).gameObject;
            pages[1] = BuildScriptPage(content).gameObject;
            pages[2] = BuildScreenPage(content).gameObject;

            // 꼬리말
            var footer = Row(panel, 16, 70);
            footerNote = Label(footer, DefaultNote, 22, Sub);
            Size(footerNote, flexW: 1);
            PillButton(footer, "종료", PillStyle.Outline, Quit, 150, 66, 27);
            PillButton(footer, "출력하기  ▶", PillStyle.Dark, StartGame, 250, 66, 28);

            SelectTab(0);
        }

        void SelectTab(int idx)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == idx);
                tabs[i].SetStyle(i == idx ? PillStyle.Dark : PillStyle.Outline);
            }
            if (idx != 1) StopPreview();
        }

        void SetNote(string text, bool error)
        {
            footerNote.text = text;
            footerNote.color = error ? Danger : Sub;
        }

        // ── 캐릭터 탭 ──

        RectTransform BuildCharacterPage(RectTransform content)
        {
            var page = Column(content, 22);
            string[] titles = { "A 캐릭터  ·  왼쪽", "B 캐릭터  ·  오른쪽" };
            string[] alias = { "A", "B" };
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                var card = CardBox(page, titles[i], $"대본에서 <b>{alias[i]}:</b> 또는 아래 이름으로 시작하는 대사가 이 캐릭터로 출력됩니다.");
                var row = Row(card, 30);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

                // 미리보기
                var thumbBox = NewRect("Thumb", row);
                Box(thumbBox, Field, 16, Line);
                Size(thumbBox, 200, 230);
                thumbEmpty[i] = Label(thumbBox, "미리보기\n없음", 22, Sub, false, TextAnchor.MiddleCenter);
                Stretch(thumbEmpty[i].rectTransform);
                var thumbRt = NewRect("Image", thumbBox);
                Stretch(thumbRt, 10);
                thumbs[i] = thumbRt.gameObject.AddComponent<Image>();
                thumbs[i].preserveAspect = true;
                thumbs[i].raycastTarget = false;
                thumbs[i].enabled = false;

                // 입력
                var col = Column(row, 10);
                Size(col, flexW: 1);
                Label(col, "이름", 23, Sub, true);
                nameFields[i] = InputBox(col, idx == 0 ? st.charAName : st.charBName, "이름표에 표시될 이름", v =>
                {
                    if (idx == 0) st.charAName = v.Trim(); else st.charBName = v.Trim();
                    st.Save();
                });
                Label(col, "이미지 경로", 23, Sub, true);
                var pathRow = Row(col, 12);
                imageFields[i] = InputBox(pathRow, idx == 0 ? st.charAImage : st.charBImage, "예) yuna.png  또는  D:\\그림\\yuna.png", v =>
                {
                    if (idx == 0) st.charAImage = v.Trim(); else st.charBImage = v.Trim();
                    st.Save();
                    RefreshCharacter(idx);
                });
                PillButton(pathRow, "찾아보기", PillStyle.Outline, () => BrowseImage(idx), 150, 58, 24);
                imageStatus[i] = Label(col, "", 21, Sub);

                // 캐릭터별 표시 방식
                Label(col, "표시 방식  <size=20><color=#6B7280>(전신: 이미지 전체 / 초상화: 잘라서 크게)</color></size>", 23, Sub, true);
                Segmented(col, new[] { "전신", "초상화" }, (int)st.GetMode(idx), m =>
                {
                    st.SetMode(idx, (VNSettings.DisplayMode)m);
                    st.Save();
                    portraitOpts[idx].SetActive(m == 1);
                    RefreshCrop(idx);
                }, 160);

                var opts = Column(col, 12);
                portraitOpts[idx] = opts.gameObject;
                var ratioRow = Row(opts, 16);
                var ratioLabel = Label(ratioRow, "자르기 비율", 23, Ink, true);
                Size(ratioLabel, w: 190);
                Segmented(ratioRow, VNSettings.RatioLabels, st.GetRatio(idx), r =>
                {
                    st.SetRatio(idx, r);
                    st.Save();
                    RefreshCrop(idx);
                }, 120);
                SliderRow(opts, "자르는 위치", 0f, 1f, st.GetFocus(idx),
                    v => v < 0.02f ? "맨 위" : v > 0.98f ? "맨 아래" : $"{v * 100:0}%", v =>
                    {
                        st.SetFocus(idx, v);
                        st.Save();
                        RefreshCrop(idx);
                    });
                portraitOpts[idx].SetActive(st.GetMode(idx) == VNSettings.DisplayMode.Portrait);
            }
            var tip = CardBox(page, null, null);
            Label(tip, "· 이미지 경로는 <b>VN/이미지</b> 폴더 기준 파일 이름(예: yuna.png) 또는 전체 경로를 입력합니다.\n" +
                       "· 배경이 투명한 PNG 를 권장합니다. 말하지 않는 캐릭터는 자동으로 어둡게 표시됩니다.\n" +
                       "· 대본에서 <b>(가운데)</b> 처럼 위치를 적으면 기본 위치 대신 그 위치에 출력됩니다.", 22, Sub);
            return page;
        }

        void BrowseImage(int idx)
        {
            var path = VNFileDialog.OpenFile("캐릭터 이미지 선택", "이미지", new[] { "png", "jpg", "jpeg" }, VNContent.Folder(VNContent.ImageFolder));
            if (path == null) return;
            path = MakeRelative(path, VNContent.ImageFolder);
            imageFields[idx].text = path;
            if (idx == 0) st.charAImage = path; else st.charBImage = path;
            st.Save();
            RefreshCharacter(idx);
        }

        void RefreshCharacter(int idx)
        {
            string path = idx == 0 ? st.charAImage : st.charBImage;
            var resolved = VNContent.ResolveFile(path, VNContent.ImageFolder);
            var status = imageStatus[idx];
            if (string.IsNullOrWhiteSpace(path)) { status.text = "이미지 없이 이름표만 표시됩니다."; status.color = Sub; }
            else if (resolved == null && !path.Contains("://")) { status.text = "✕ 파일을 찾을 수 없습니다: " + path; status.color = Danger; }
            else { status.text = "✓ " + ShortPath(resolved ?? path); status.color = Teal; }

            cropSources[idx] = null;
            RefreshCrop(idx);
            if (string.IsNullOrWhiteSpace(path)) return;
            StartCoroutine(VNImageLoader.Load(path, s =>
            {
                cropSources[idx] = s;
                RefreshCrop(idx);
            }));
        }

        // ── 대사 · 배경음 탭 ──

        RectTransform BuildScriptPage(RectTransform content)
        {
            var page = Column(content, 22);

            var card = CardBox(page, "대사 파일", "VN/대사 폴더에 메모장(txt) 파일을 넣거나, 다른 위치의 파일을 직접 첨부할 수 있습니다.");
            Label(card, "폴더에서 선택", 24, Ink, true);
            var r1 = Row(card, 12);
            scriptSel = SelectorBox(r1, "VN/대사 폴더에 txt 파일이 없습니다", i =>
            {
                st.folderScript = scriptSel.options[i];
                st.Save();
                RefreshScriptStatus();
            });
            PillButton(r1, "폴더 열기", PillStyle.Outline, () => VNContent.OpenFolder(VNContent.ScriptFolder), 150, 58, 24);

            Label(card, "직접 첨부  <size=20><color=#6B7280>(첨부한 파일이 있으면 폴더 선택보다 우선)</color></size>", 24, Ink, true);
            var r2 = Row(card, 12);
            attachField = InputBox(r2, st.attachedScript, "첨부한 파일 없음", v =>
            {
                st.attachedScript = v.Trim().Trim('"');
                st.Save();
                RefreshScriptStatus();
            });
            PillButton(r2, "찾아보기", PillStyle.Outline, BrowseScript, 150, 58, 24);
            PillButton(r2, "해제", PillStyle.Outline, () =>
            {
                attachField.text = "";
                st.attachedScript = "";
                st.Save();
                RefreshScriptStatus();
            }, 100, 58, 24);
            scriptStatus = Label(card, "", 23, Sub);

            var bcard = CardBox(page, "배경음", "VN/배경음 폴더에 mp3 / ogg / wav 파일이 있으면 게임 중에 반복 재생됩니다.  \"없음\" 을 고르면 재생하지 않습니다.");
            var r3 = Row(bcard, 12);
            bgmSel = SelectorBox(r3, BgmNone, i =>
            {
                // 0 번은 항상 "없음"
                st.bgmEnabled = i > 0;
                if (i > 0) st.bgmFile = bgmSel.options[i];
                st.Save();
                RefreshBgmStatus();
                if (previewingBgm)
                {
                    if (i == 0) StopPreview();
                    else StartCoroutine(PlayBgm(st.ResolveBgmPath(), false));
                }
            });
            previewBgmButton = PillButton(r3, "▶ 미리 듣기", PillStyle.Outline, TogglePreview, 170, 58, 24);
            PillButton(r3, "폴더 열기", PillStyle.Outline, () => VNContent.OpenFolder(VNContent.BgmFolder), 150, 58, 24);
            volumeSlider = SliderRow(bcard, "볼륨", 0f, 1f, st.bgmVolume, v => $"{v * 100:0}%", v =>
            {
                st.bgmVolume = v;
                bgm.volume = v;
                st.Save();
            });
            bgmStatus = Label(bcard, "", 23, Sub);
            return page;
        }

        void BrowseScript()
        {
            var path = VNFileDialog.OpenFile("대사 파일 선택", "대사 파일", new[] { "txt", "json" }, VNContent.Folder(VNContent.ScriptFolder));
            if (path == null) return;
            attachField.text = path;
            st.attachedScript = path;
            st.Save();
            RefreshScriptStatus();
        }

        void RefreshScriptList()
        {
            var files = VNContent.ListFiles(VNContent.ScriptFolder, VNContent.ScriptExtensions);
            var names = System.Array.ConvertAll(files, Path.GetFileName);
            int sel = System.Array.IndexOf(names, st.folderScript);
            scriptSel.SetOptions(names, sel < 0 ? 0 : sel);
            if (names.Length > 0 && sel < 0) st.folderScript = names[0];
        }

        void RefreshScriptStatus()
        {
            var attached = st.attachedScript?.Trim();
            bool attachMissing = !string.IsNullOrEmpty(attached) && VNContent.ResolveFile(attached, VNContent.ScriptFolder) == null;
            var path = st.ResolveScriptPath();
            if (path == null)
            {
                scriptStatus.text = "✕ 사용할 대사 파일이 없습니다.";
                scriptStatus.color = Danger;
                return;
            }

            int count = 0;
            try
            {
                var text = VNTextParser.DecodeText(File.ReadAllBytes(path));
                if (text.TrimStart().StartsWith("{"))
                    count = JsonUtility.FromJson<VNScript>(text)?.lines?.Count ?? 0;
                else
                    count = VNTextParser.Parse(text).lines.Count;
            }
            catch { count = -1; }

            string info = count < 0 ? "읽을 수 없음" : $"대사 {count}개";
            scriptStatus.text = (attachMissing ? "<color=#E5484D>첨부 파일을 찾을 수 없어 폴더 파일을 사용합니다.</color>\n" : "") +
                                $"▶ 사용할 대사: <b>{Path.GetFileName(path)}</b>  ·  {info}";
            scriptStatus.color = count > 0 ? Teal : Danger;
        }

        const string BgmNone = "없음";

        void RefreshBgmList()
        {
            var files = VNContent.ListFiles(VNContent.BgmFolder, VNContent.AudioExtensions);
            var names = new string[files.Length + 1];
            names[0] = BgmNone;
            for (int i = 0; i < files.Length; i++) names[i + 1] = Path.GetFileName(files[i]);

            int sel;
            if (!st.bgmEnabled || files.Length == 0) sel = 0;
            else
            {
                sel = System.Array.IndexOf(names, st.bgmFile, 1);
                if (sel < 0) { sel = 1; st.bgmFile = names[1]; } // 고른 파일이 없어졌으면 첫 번째 파일
            }
            bgmSel.SetOptions(names, sel);
        }

        void RefreshBgmStatus()
        {
            bool hasFiles = VNContent.ListFiles(VNContent.BgmFolder, VNContent.AudioExtensions).Length > 0;
            var path = st.ResolveBgmPath();
            if (!hasFiles) { bgmStatus.text = "폴더에 배경음이 없습니다. 배경음 없이 실행됩니다."; bgmStatus.color = Sub; }
            else if (path == null) { bgmStatus.text = "배경음 없음 — 게임 중에 배경음을 재생하지 않습니다."; bgmStatus.color = Sub; }
            else { bgmStatus.text = $"♪ 재생할 배경음: <b>{Path.GetFileName(path)}</b>"; bgmStatus.color = Teal; }
        }

        void TogglePreview()
        {
            if (previewingBgm) { StopPreview(); return; }
            var path = st.ResolveBgmPath();
            if (path == null) return;
            previewingBgm = true;
            previewBgmButton.label.text = "■ 정지";
            previewBgmButton.SetStyle(PillStyle.Teal);
            StartCoroutine(PlayBgm(path, false));
        }

        void StopPreview()
        {
            if (!previewingBgm) return;
            previewingBgm = false;
            previewBgmButton.label.text = "▶ 미리 듣기";
            previewBgmButton.SetStyle(PillStyle.Outline);
            StopBgm();
        }

        // ── 화면 탭 ──

        RectTransform BuildScreenPage(RectTransform content)
        {
            var page = Column(content, 22);

            var scard = CardBox(page, "인물 크기", "화면에 출력되는 인물 이미지의 크기입니다.");
            SliderRow(scard, "크기", 50f, 150f, st.characterScale * 100f, v => $"{v:0}%", v =>
            {
                st.characterScale = v / 100f;
                st.Save();
            });

            var tcard = CardBox(page, "대사 속도", "글자가 한 글자씩 출력되는 속도입니다.");
            Segmented(tcard, new[] { "느리게", "보통", "빠르게", "즉시" }, st.textSpeed, i =>
            {
                st.textSpeed = i;
                st.Save();
            }, 170);

            var wcard = CardBox(page, "창", null);
            ToggleRow(wcard, "전체 화면", "끄면 1280×720 창 모드로 실행됩니다.", st.fullscreen, v =>
            {
                st.fullscreen = v;
                st.Save();
                ApplyWindowMode();
            });

            var kcard = CardBox(page, "게임 중 조작", null);
            Label(kcard, "· 클릭 / Space / Enter : 다음 대사\n· ESC 또는 오른쪽 위 ✕ 버튼 : 메뉴 (돌아가기 / 종료하기)\n· F5 : 대사·이미지 파일 다시 불러오기", 22, Sub);
            return page;
        }

        /// <summary>캐릭터 미리보기: 초상화면 자른 결과, 전신이면 원본</summary>
        void RefreshCrop(int idx)
        {
            var img = thumbs[idx];
            if (img == null) return;
            var s = cropSources[idx];
            // 이전에 만든 미리보기용 잘린 스프라이트 정리
            if (img.sprite != null && img.sprite != cropSources[0] && img.sprite != cropSources[1]) Destroy(img.sprite);
            if (s != null && st.GetMode(idx) == VNSettings.DisplayMode.Portrait)
                s = VNPlayer.CropSprite(s, VNSettings.AspectOf(st.GetRatio(idx)), st.GetFocus(idx));
            img.sprite = s;
            img.enabled = s != null;
            thumbEmpty[idx].enabled = s == null;
        }

        void ApplyWindowMode()
        {
            if (Application.isEditor) return;
            if (st.fullscreen)
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else if (Screen.fullScreen || Screen.width != 1280)
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        }

        void RefreshAll()
        {
            VNImageLoader.ClearCache();
            RefreshCharacter(0);
            RefreshCharacter(1);
            RefreshScriptList();
            RefreshScriptStatus();
            RefreshBgmList();
            RefreshBgmStatus();
        }

        // ───────────────────────── 게임 중 UI ─────────────────────────

        void BuildHud()
        {
            hudCanvas = MakeCanvas("HUD Canvas", 20);
            var btnRt = NewRect("CloseButton", hudCanvas.transform);
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(1, 1);
            btnRt.pivot = new Vector2(1, 1);
            btnRt.sizeDelta = new Vector2(68, 68);
            btnRt.anchoredPosition = new Vector2(-28, -28);
            var img = RoundImage(btnRt, new Color(0, 0, 0, 0.45f), 34);
            var x = Label(btnRt, "✕", 32, Color.white, true, TextAnchor.MiddleCenter);
            Stretch(x.rectTransform);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { if (paused) Resume(); else Pause(); });
        }

        void BuildPause()
        {
            pauseCanvas = MakeCanvas("Pause Canvas", 30);
            var dim = NewRect("Dim", pauseCanvas.transform);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);

            var card = NewRect("Card", pauseCanvas.transform);
            card.sizeDelta = new Vector2(640, 0);
            Box(card, Card, 26, Line);
            var v = VLayout(card, 26, new RectOffset(48, 48, 44, 44));
            v.childAlignment = TextAnchor.UpperCenter;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Label(card, "메뉴", 38, Ink, true, TextAnchor.MiddleCenter);
            Label(card, "ESC 를 다시 누르면 계속 진행합니다.", 23, Sub, false, TextAnchor.MiddleCenter);
            var row = Row(card, 18, 70);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            PillButton(row, "돌아가기", PillStyle.Outline, ShowMenu, 240, 68, 27);
            PillButton(row, "종료하기", PillStyle.Dark, Quit, 240, 68, 27);
        }

        // ───────────────────────── 배경음 ─────────────────────────

        IEnumerator PlayBgm(string path, bool fromStart)
        {
            if (string.IsNullOrEmpty(path)) { StopBgm(); yield break; }
            System.DateTime time;
            try { time = File.GetLastWriteTimeUtc(path); } catch { time = default; }

            if (bgm.clip == null || bgmLoadedPath != path || bgmLoadedTime != time)
            {
                var type = AudioTypeOf(path);
                using (var req = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(path).AbsoluteUri, type))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[VN] 배경음 로드 실패: {path} - {req.error}");
                        if (!inGame) SetNote("배경음을 불러오지 못했습니다: " + Path.GetFileName(path), true);
                        yield break;
                    }
                    var clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = Path.GetFileNameWithoutExtension(path);
                    if (bgm.clip != null && bgm.clip != clip) Destroy(bgm.clip);
                    bgm.clip = clip;
                    bgmLoadedPath = path;
                    bgmLoadedTime = time;
                }
            }
            bgm.volume = st.bgmVolume;
            if (fromStart || !bgm.isPlaying) { bgm.Stop(); bgm.Play(); }
        }

        void StopBgm()
        {
            if (bgm != null) bgm.Stop();
        }

        static AudioType AudioTypeOf(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3": return AudioType.MPEG;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".wav": return AudioType.WAV;
                default: return AudioType.UNKNOWN;
            }
        }

        // ───────────────────────── 유틸 ─────────────────────────

        Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>콘텐츠 폴더 안의 파일이면 "VN/이미지/a.png" 처럼 짧게 표시</summary>
        static string ShortPath(string full)
        {
            try
            {
                var root = Path.GetFullPath(VNContent.Root ?? VNContent.BuiltinRoot).TrimEnd('\\', '/');
                var f = Path.GetFullPath(full);
                if (f.StartsWith(root + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase))
                    return "VN/" + f.Substring(root.Length + 1).Replace('\\', '/');
            }
            catch { }
            return full;
        }

        /// <summary>콘텐츠 하위 폴더 안의 파일이면 파일 이름만, 아니면 전체 경로</summary>
        static string MakeRelative(string fullPath, string sub)
        {
            try
            {
                var dir = Path.GetFullPath(VNContent.Folder(sub)).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(fullPath);
                if (full.StartsWith(dir, System.StringComparison.OrdinalIgnoreCase)) return full.Substring(dir.Length);
            }
            catch { }
            return fullPath;
        }
    }
}
