using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VisualNovel
{
    /// <summary>
    /// UI 설정 탭에서 고른 값(대사 박스 / 네임 박스 테두리 이미지, 대사 폰트, 글자색)을 실제 리소스로 만든 것.
    /// 이미지는 VN/UI 폴더에서 고르며, 비워 두면 기본 UI 를 그대로 쓴다.
    /// </summary>
    public class VNSkin
    {
        public Sprite dialogueBox, namePlate;
        public int dialogueBoxBorder, namePlateBorder;
        public Font font;
        public Color? textColor;

        /// <summary>VN/UI 폴더의 이미지 파일 이름 목록</summary>
        public static string[] ListImages()
        {
            var files = VNContent.ListFiles(VNContent.UIFolder, VNContent.ImageExtensions);
            return Array.ConvertAll(files, Path.GetFileName);
        }

        /// <summary>설치된 OS 폰트 이름 목록 (이름순, 중복 제거)</summary>
        public static string[] ListFonts()
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var f in Font.GetOSInstalledFontNames()) if (!string.IsNullOrWhiteSpace(f)) set.Add(f); }
            catch (Exception e) { Debug.LogWarning($"[VN] 폰트 목록을 가져오지 못했습니다: {e.Message}"); }
            var arr = new string[set.Count];
            set.CopyTo(arr);
            return arr;
        }

        static readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();

        public static Font GetFont(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (fontCache.TryGetValue(name, out var f) && f != null) return f;
            // 한글이 없는 폰트를 골라도 글자가 사라지지 않도록 한글 폰트를 뒤에 둠
            f = Font.CreateDynamicFontFromOSFont(new[] { name, "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 40);
            fontCache[name] = f;
            return f;
        }

        /// <summary>설정 값으로 스킨 만들기. 아무것도 지정하지 않았으면 null (= 기본 UI)</summary>
        public static IEnumerator Build(VNSettings st, Action<VNSkin> done)
        {
            var skin = new VNSkin
            {
                dialogueBoxBorder = st.uiBoxBorder,
                namePlateBorder = st.uiPlateBorder,
                font = GetFont(st.uiFont),
            };
            if (!string.IsNullOrWhiteSpace(st.uiTextColor) && ColorUtility.TryParseHtmlString(st.uiTextColor.Trim(), out var c))
                skin.textColor = c;

            yield return LoadSprite(st.uiBoxImage, st.uiBoxBorder, s => skin.dialogueBox = s);
            yield return LoadSprite(st.uiPlateImage, st.uiPlateBorder, s => skin.namePlate = s);

            bool empty = skin.dialogueBox == null && skin.namePlate == null && skin.font == null && skin.textColor == null;
            done(empty ? null : skin);
        }

        /// <summary>VN/UI 폴더(또는 전체 경로)의 이미지를 9-slice 여백(border, px)을 넣어 스프라이트로 만듦</summary>
        public static IEnumerator LoadSprite(string file, int border, Action<Sprite> set)
        {
            if (string.IsNullOrWhiteSpace(file)) yield break;
            var path = VNContent.ResolveFile(file, VNContent.UIFolder);
            if (path == null) { Debug.LogWarning($"[VN] UI 이미지를 찾을 수 없습니다: {file}"); yield break; }

            using (var req = UnityWebRequestTexture.GetTexture(new Uri(path).AbsoluteUri))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[VN] UI 이미지 로드 실패: {path} - {req.error}");
                    yield break;
                }
                var tex = DownloadHandlerTexture.GetContent(req);
                tex.wrapMode = TextureWrapMode.Clamp;
                int bx = Mathf.Clamp(border, 0, tex.width / 2 - 1), by = Mathf.Clamp(border, 0, tex.height / 2 - 1);
                var b = border > 0 ? new Vector4(bx, by, bx, by) : Vector4.zero;
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, b);
                sprite.name = Path.GetFileNameWithoutExtension(path);
                set(sprite);
            }
        }
    }
}
