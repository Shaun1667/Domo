using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VisualNovel
{
    /// <summary>
    /// 설정 화면용 uGUI 위젯을 코드로 만드는 도구 모음.
    /// 둥근 카드 / 알약 버튼 / 토글 스위치 / 입력칸 / 슬라이더 / 좌우 선택기.
    /// </summary>
    public static class VNUIKit
    {
        // ───────── 색상 ─────────
        public static readonly Color Bg = Hex("#F3F4F6");
        public static readonly Color Card = Color.white;
        public static readonly Color Line = Hex("#E3E5E8");
        public static readonly Color Teal = Hex("#14B8A6");
        public static readonly Color Ink = Hex("#111111");
        public static readonly Color Sub = Hex("#6B7280");
        public static readonly Color Field = Hex("#F7F8FA");
        public static readonly Color Danger = Hex("#E5484D");
        public static readonly Color Off = Hex("#D9DCE1");

        public static Font font;

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // ───────── 둥근 스프라이트 ─────────
        const int R = 64;
        static Sprite roundSprite;

        /// <summary>반지름 64px 원/알약 9-slice 스프라이트 (pixelsPerUnitMultiplier 로 반지름 조절)</summary>
        public static Sprite Round
        {
            get
            {
                if (roundSprite != null) return roundSprite;
                int size = R * 2 + 1;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - R) - 0.5f);
                    float dy = Mathf.Max(0, Mathf.Abs(y - R) - 0.5f);
                    float a = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                roundSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(R, R, R, R));
                roundSprite.name = "VNRound";
                return roundSprite;
            }
        }

        // ───────── 기본 ─────────

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static LayoutElement Size(Component c, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static Image RoundImage(RectTransform rt, Color color, float radius)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = R / Mathf.Max(1f, radius);
            img.color = color;
            return img;
        }

        /// <summary>테두리 있는 둥근 상자. 반환값은 안쪽(채움) 이미지</summary>
        public static Image Box(RectTransform rt, Color fill, float radius, Color? border = null, float borderWidth = 1.5f)
        {
            if (border == null) return RoundImage(rt, fill, radius);
            var outer = RoundImage(rt, border.Value, radius);
            outer.raycastTarget = true;
            var innerRt = NewRect("Fill", rt);
            Stretch(innerRt, borderWidth);
            var inner = RoundImage(innerRt, fill, radius - borderWidth);
            inner.raycastTarget = false;
            innerRt.SetAsFirstSibling();
            innerRt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return inner;
        }

        public static VerticalLayoutGroup VLayout(RectTransform rt, float spacing, RectOffset padding = null, bool expandW = true)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childControlWidth = true; g.childControlHeight = true;
            g.childForceExpandWidth = expandW; g.childForceExpandHeight = false;
            g.childAlignment = TextAnchor.UpperLeft;
            return g;
        }

        public static HorizontalLayoutGroup HLayout(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childControlWidth = true; g.childControlHeight = true;
            g.childForceExpandWidth = false; g.childForceExpandHeight = false;
            g.childAlignment = align;
            return g;
        }

        public static Text Label(Transform parent, string text, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        // ───────── 카드 ─────────

        /// <summary>흰색 둥근 카드. 반환값은 내용을 넣을 세로 레이아웃 영역</summary>
        public static RectTransform CardBox(Transform parent, string title, string desc = null)
        {
            var rt = NewRect("Card", parent);
            Box(rt, Card, 22, Line);
            VLayout(rt, 16, new RectOffset(32, 32, 26, 28));
            if (!string.IsNullOrEmpty(title)) Label(rt, title, 30, Ink, true);
            if (!string.IsNullOrEmpty(desc)) Label(rt, desc, 22, Sub);
            return rt;
        }

        public static RectTransform Row(Transform parent, float spacing = 14, float height = -1)
        {
            var rt = NewRect("Row", parent);
            HLayout(rt, spacing);
            if (height > 0) Size(rt, h: height);
            return rt;
        }

        public static RectTransform Column(Transform parent, float spacing = 10)
        {
            var rt = NewRect("Column", parent);
            VLayout(rt, spacing);
            return rt;
        }

        // ───────── 알약 버튼 ─────────

        public enum PillStyle { Outline, Teal, Dark }

        public class Pill : MonoBehaviour
        {
            public Image outer, inner;
            public Text label;
            public Button button;

            public void SetStyle(PillStyle s)
            {
                switch (s)
                {
                    case PillStyle.Teal: outer.color = Teal; inner.color = Teal; label.color = Color.white; break;
                    case PillStyle.Dark: outer.color = Ink; inner.color = Ink; label.color = Color.white; break;
                    default: outer.color = Line; inner.color = Color.white; label.color = Ink; break;
                }
            }
        }

        public static Pill PillButton(Transform parent, string text, PillStyle style, UnityAction onClick, float width = -1, float height = 60, int fontSize = 26)
        {
            var rt = NewRect("Button", parent);
            var p = rt.gameObject.AddComponent<Pill>();
            p.outer = RoundImage(rt, Line, height / 2f);
            var innerRt = NewRect("Fill", rt);
            Stretch(innerRt, 1.5f);
            p.inner = RoundImage(innerRt, Color.white, height / 2f - 1.5f);
            p.inner.raycastTarget = false;
            p.label = Label(rt, text, fontSize, Ink, true, TextAnchor.MiddleCenter);
            Stretch(p.label.rectTransform);
            p.button = rt.gameObject.AddComponent<Button>();
            p.button.targetGraphic = p.outer;
            var colors = p.button.colors;
            colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            p.button.colors = colors;
            if (onClick != null) p.button.onClick.AddListener(onClick);
            p.SetStyle(style);
            if (width > 0) Size(rt, w: width, h: height);
            else Size(rt, h: height, w: p.label.preferredWidth + 64);
            return p;
        }

        /// <summary>여러 개 중 하나를 고르는 알약 버튼 묶음 (선택된 것은 청록색)</summary>
        public static Pill[] Segmented(Transform parent, string[] labels, int selected, Action<int> onChange, float itemWidth = 200)
        {
            var row = Row(parent, 16, 62);
            var pills = new Pill[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                pills[i] = PillButton(row, labels[i], PillStyle.Outline, null, itemWidth);
                pills[i].button.onClick.AddListener(() =>
                {
                    for (int k = 0; k < pills.Length; k++) pills[k].SetStyle(k == idx ? PillStyle.Teal : PillStyle.Outline);
                    onChange?.Invoke(idx);
                });
            }
            for (int k = 0; k < pills.Length; k++) pills[k].SetStyle(k == selected ? PillStyle.Teal : PillStyle.Outline);
            return pills;
        }

        // ───────── 토글 스위치 ─────────

        public class Switch : MonoBehaviour
        {
            public Image track;
            public RectTransform knob;
            public bool value;
            public Action<bool> onChange;

            public void Set(bool v, bool notify)
            {
                value = v;
                track.color = v ? Teal : Off;
                knob.anchoredPosition = new Vector2(v ? 15 : -15, 0);
                if (notify) onChange?.Invoke(v);
            }
        }

        public static Switch ToggleRow(Transform parent, string title, string desc, bool value, Action<bool> onChange)
        {
            var row = Row(parent, 22);
            var swRt = NewRect("Switch", row);
            Size(swRt, 72, 40);
            var sw = swRt.gameObject.AddComponent<Switch>();
            sw.track = RoundImage(swRt, Off, 20);
            sw.knob = NewRect("Knob", swRt);
            sw.knob.sizeDelta = new Vector2(32, 32);
            RoundImage(sw.knob, Color.white, 16).raycastTarget = false;
            var btn = swRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = sw.track;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => sw.Set(!sw.value, true));

            var col = Column(row, 4);
            Size(col, flexW: 1);
            Label(col, title, 26, Ink, true);
            if (!string.IsNullOrEmpty(desc)) Label(col, desc, 21, Sub);

            sw.Set(value, false);
            sw.onChange = onChange;
            return sw;
        }

        // ───────── 입력칸 ─────────

        public static InputField InputBox(Transform parent, string value, string placeholder, Action<string> onEndEdit, float height = 58)
        {
            var rt = NewRect("Input", parent);
            Box(rt, Field, 14, Line);
            Size(rt, h: height, flexW: 1);

            var text = Label(rt, "", 24, Ink);
            var textRt = text.rectTransform;
            Stretch(textRt);
            textRt.offsetMin = new Vector2(20, 0);
            textRt.offsetMax = new Vector2(-20, 0);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var ph = Label(rt, placeholder, 24, new Color(0.6f, 0.62f, 0.66f));
            Stretch(ph.rectTransform);
            ph.rectTransform.offsetMin = new Vector2(20, 0);
            ph.rectTransform.offsetMax = new Vector2(-20, 0);
            ph.fontStyle = FontStyle.Normal;

            var field = rt.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            field.lineType = InputField.LineType.SingleLine;
            // 기본 커서/선택 영역은 엉뚱한 위치에 그려지는 경우가 있어 숨기고 Caret 으로 직접 그림
            field.customCaretColor = true;
            field.caretColor = Color.clear;
            field.caretWidth = 2;
            field.selectionColor = Color.clear;
            field.transition = Selectable.Transition.None;
            field.text = value ?? "";
            if (onEndEdit != null) field.onEndEdit.AddListener(v => onEndEdit(v));

            var barRt = NewRect("Caret", textRt);
            var bar = barRt.gameObject.AddComponent<Image>();
            bar.color = Ink;
            bar.raycastTarget = false;
            bar.enabled = false;
            var caret = rt.gameObject.AddComponent<Caret>();
            caret.field = field;
            caret.bar = bar;
            return field;
        }

        /// <summary>
        /// 입력칸 커서. 글자를 그리는 것과 같은 TextGenerator 의 글자 위치를 써서
        /// 커서가 항상 마지막으로 입력한 글자 바로 뒤에 오도록 한다 (한글 조합 중인 글자 포함).
        /// </summary>
        public class Caret : MonoBehaviour
        {
            public InputField field;
            public Image bar;
            const float BlinkRate = 0.53f;
            float timer;
            int lastIndex = -1, drawStart;

            void LateUpdate()
            {
                bool show = field != null && field.isFocused && field.selectionAnchorPosition == field.selectionFocusPosition;
                if (!show) { bar.enabled = false; timer = 0f; lastIndex = -1; return; }

                var text = field.textComponent;
                var gen = text.cachedTextGenerator;
                string comp = CompositionString();
                string shown = text.text ?? "";

                // 긴 텍스트가 옆으로 밀린 경우 보이는 부분의 시작 위치
                if (comp.Length == 0)
                {
                    int k = shown.Length > 0 ? field.text.IndexOf(shown, StringComparison.Ordinal) : 0;
                    drawStart = Mathf.Max(0, k);
                }

                int index = field.caretPosition - drawStart + comp.Length;
                float x;
                if (gen.characterCount == 0) x = text.rectTransform.rect.xMin;
                else x = gen.characters[Mathf.Clamp(index, 0, gen.characterCount - 1)].cursorPos.x / text.pixelsPerUnit;

                var r = text.rectTransform.rect;
                var rt = bar.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(2f, text.fontSize * 1.25f);
                rt.localPosition = new Vector3(x, r.center.y, 0f);

                if (index != lastIndex) { timer = 0f; lastIndex = index; } // 움직이면 깜빡임 처음부터
                timer += Time.unscaledDeltaTime;
                bar.enabled = (timer % (BlinkRate * 2f)) < BlinkRate;
            }

            static string CompositionString()
            {
                try
                {
                    var module = UnityEngine.EventSystems.EventSystem.current?.currentInputModule;
                    return module != null && module.input != null ? module.input.compositionString ?? "" : "";
                }
                catch { return ""; }
            }
        }

        // ───────── 슬라이더 ─────────

        public static Slider SliderRow(Transform parent, string title, float min, float max, float value, Func<float, string> format, Action<float> onChange)
        {
            var row = Row(parent, 22, 48);
            var t = Label(row, title, 25, Ink, true);
            Size(t, w: 190);

            var rt = NewRect("Slider", row);
            Size(rt, h: 40, flexW: 1);
            var bg = NewRect("Track", rt);
            bg.anchorMin = new Vector2(0, 0.5f); bg.anchorMax = new Vector2(1, 0.5f);
            bg.sizeDelta = new Vector2(0, 10);
            RoundImage(bg, Off, 5);

            var fillArea = NewRect("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 10);
            var fill = NewRect("Fill", fillArea);
            fill.sizeDelta = Vector2.zero;
            RoundImage(fill, Teal, 5);

            var handleArea = NewRect("Handle Area", rt);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(17, 3);
            handleArea.offsetMax = new Vector2(-17, -3);
            var handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(34, 0);
            var hOuter = RoundImage(handle, Teal, 17);
            var hInner = NewRect("Dot", handle);
            Stretch(hInner, 5);
            RoundImage(hInner, Color.white, 12).raycastTarget = false;

            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = hOuter;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;

            var valueLabel = Label(row, format(value), 24, Sub, false, TextAnchor.MiddleRight);
            Size(valueLabel, w: 110);
            s.onValueChanged.AddListener(v => { valueLabel.text = format(v); onChange?.Invoke(v); });
            return s;
        }

        // ───────── 좌우 선택기 ─────────

        public class Selector : MonoBehaviour
        {
            public Text label;
            public string[] options = new string[0];
            public int index;
            public Action<int> onChange;
            public string emptyText = "(없음)";

            public void SetOptions(string[] opts, int selected)
            {
                options = opts ?? new string[0];
                index = options.Length == 0 ? -1 : Mathf.Clamp(selected, 0, options.Length - 1);
                Refresh();
            }

            public void Step(int d)
            {
                if (options.Length == 0) return;
                index = (index + d + options.Length) % options.Length;
                Refresh();
                onChange?.Invoke(index);
            }

            void Refresh()
            {
                label.text = index < 0 ? emptyText : options[index];
                label.color = index < 0 ? Sub : Ink;
            }
        }

        public static Selector SelectorBox(Transform parent, string emptyText, Action<int> onChange)
        {
            var rt = NewRect("Selector", parent);
            Box(rt, Field, 14, Line);
            Size(rt, h: 58, flexW: 1);
            HLayout(rt, 8, new RectOffset(8, 8, 6, 6));
            var sel = rt.gameObject.AddComponent<Selector>();
            sel.emptyText = emptyText;

            var prev = PillButton(rt, "◀", PillStyle.Outline, () => sel.Step(-1), 56, 46, 20);
            sel.label = Label(rt, emptyText, 24, Ink, false, TextAnchor.MiddleCenter);
            sel.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Size(sel.label, flexW: 1);
            var next = PillButton(rt, "▶", PillStyle.Outline, () => sel.Step(1), 56, 46, 20);
            sel.onChange = onChange;
            return sel;
        }

        // ───────── 스크롤 영역 ─────────

        /// <summary>세로 스크롤 영역. 반환값은 내용을 넣을 Content</summary>
        public static RectTransform ScrollArea(RectTransform rt)
        {
            var sr = rt.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;

            var viewport = NewRect("Viewport", rt);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0); // 빈 곳에서도 휠 스크롤 되도록

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            VLayout(content, 22, new RectOffset(2, 2, 2, 24));
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = viewport;
            sr.content = content;
            return content;
        }
    }
}
