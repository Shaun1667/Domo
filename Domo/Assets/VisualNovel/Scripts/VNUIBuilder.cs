using UnityEngine;
using UnityEngine.UI;

namespace VisualNovel
{
    /// <summary>
    /// VNPlayer 에 필요한 UI 계층을 코드로 생성한다.
    /// 에디터 메뉴(Tools/Visual Novel/Create VN Player)와 런타임(참조가 비어 있을 때) 양쪽에서 사용.
    /// </summary>
    public static class VNUIBuilder
    {
        public static Font DefaultFont()
        {
            return Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Arial" }, 32);
        }

        public static void Build(VNPlayer p)
        {
            var root = p.gameObject;
            if (!root.TryGetComponent<Canvas>(out var canvas)) canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (!root.TryGetComponent<CanvasScaler>(out var scaler)) scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 씬에 저장 가능한 내장 폰트로 만들고, 실행 시 VNPlayer 가 OS 한글 폰트로 교체한다
            var font = p.font != null ? p.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 배경 (화면을 꽉 채우도록 Envelope)
            var bgArea = Rect("BackgroundArea", root.transform);
            Stretch(bgArea);
            bgArea.gameObject.AddComponent<Image>().color = Color.black;
            var bg = Rect("Background", bgArea);
            Stretch(bg);
            p.backgroundImage = bg.gameObject.AddComponent<Image>();
            p.backgroundImage.enabled = false;
            p.backgroundFitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            p.backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            p.backgroundFitter.aspectRatio = 16f / 9f;

            // 인물 슬롯
            var chars = Rect("Characters", root.transform);
            Stretch(chars);
            p.characterSlots = new Image[3];
            float[] xs = { 0.2f, 0.5f, 0.8f };
            string[] names = { "Left", "Center", "Right" };
            for (int i = 0; i < 3; i++)
            {
                var slot = Rect(names[i], chars);
                slot.anchorMin = new Vector2(xs[i], 0f);
                slot.anchorMax = new Vector2(xs[i], 0.92f);
                slot.pivot = new Vector2(0.5f, 0f);
                slot.sizeDelta = new Vector2(600, 0);
                slot.anchoredPosition = Vector2.zero;
                var img = slot.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.enabled = false;
                var fit = slot.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fit.aspectRatio = 0.5f;
                p.characterSlots[i] = img;
            }

            // 대화창
            var box = Rect("DialogueBox", root.transform);
            box.anchorMin = new Vector2(0f, 0f);
            box.anchorMax = new Vector2(1f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            box.offsetMin = new Vector2(60, 30);
            box.offsetMax = new Vector2(-60, 330);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = new Color(0.05f, 0.05f, 0.1f, 0.82f);
            p.dialogueBox = box.gameObject;

            // 이름표
            var plate = Rect("NamePlate", box);
            plate.anchorMin = plate.anchorMax = new Vector2(0f, 1f);
            plate.pivot = new Vector2(0f, 0.5f);
            plate.sizeDelta = new Vector2(340, 70);
            plate.anchoredPosition = new Vector2(40, 0);
            plate.gameObject.AddComponent<Image>().color = new Color(0.32f, 0.2f, 0.45f, 0.95f);
            p.namePlate = plate.gameObject;

            var nameRt = Rect("NameText", plate);
            Stretch(nameRt);
            nameRt.offsetMin = new Vector2(20, 0);
            nameRt.offsetMax = new Vector2(-20, 0);
            p.nameText = MakeText(nameRt, font, 40, TextAnchor.MiddleCenter, FontStyle.Bold);

            // 본문
            var textRt = Rect("DialogueText", box);
            Stretch(textRt);
            textRt.offsetMin = new Vector2(60, 40);
            textRt.offsetMax = new Vector2(-60, -60);
            p.dialogueText = MakeText(textRt, font, 40, TextAnchor.UpperLeft, FontStyle.Normal);
            p.dialogueText.lineSpacing = 1.2f;

            // 다음 표시
            var nextRt = Rect("NextIndicator", box);
            nextRt.anchorMin = nextRt.anchorMax = new Vector2(1f, 0f);
            nextRt.pivot = new Vector2(1f, 0f);
            nextRt.sizeDelta = new Vector2(60, 60);
            nextRt.anchoredPosition = new Vector2(-30, 20);
            var next = MakeText(nextRt, font, 36, TextAnchor.MiddleCenter, FontStyle.Normal);
            next.text = "▼";
            p.nextIndicator = next.gameObject;
            p.nextIndicator.SetActive(false);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Text MakeText(RectTransform rt, Font font, int size, TextAnchor align, FontStyle style)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }
    }
}
