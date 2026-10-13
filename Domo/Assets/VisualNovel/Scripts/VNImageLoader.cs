using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VisualNovel
{
    /// <summary>
    /// 문자열 경로로 스프라이트를 불러온다.
    /// 지원: http(s)/file URL, 절대경로, 콘텐츠 폴더(exe 옆 VN 폴더 등) 기준 상대경로, Resources 경로.
    /// </summary>
    public static class VNImageLoader
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>상대경로 이미지의 기준 폴더 (VNContent.Root)</summary>
        public static string StreamingRoot => VNContent.Root ?? VNContent.BuiltinRoot;

        public static void ClearCache() => cache.Clear();

        public static IEnumerator Load(string path, Action<Sprite> onDone)
        {
            if (string.IsNullOrWhiteSpace(path)) { onDone(null); yield break; }
            path = path.Trim();

            if (cache.TryGetValue(path, out var cached) && cached != null) { onDone(cached); yield break; }

            // 1) 콘텐츠 폴더(또는 이미지 폴더) 파일이 있으면 우선 (외부 폴더 이미지 교체가 바로 적용되도록)
            string localFile = VNContent.ResolveFile(path, VNContent.ImageFolder);
            bool isFile = localFile != null;

            // 2) Resources
            var resPath = Path.ChangeExtension(path, null)?.Replace('\\', '/');
            var res = isFile ? null : Resources.Load<Sprite>(resPath);
            if (res == null && !isFile)
            {
                var tex = Resources.Load<Texture2D>(resPath);
                if (tex != null) res = ToSprite(tex);
            }
            if (res != null) { cache[path] = res; onDone(res); yield break; }

            // 3) URL / 파일
            string url = isFile ? new Uri(localFile).AbsoluteUri : ResolveUrl(path);
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[VN] 이미지 로드 실패: {path} ({url}) - {req.error}");
                    onDone(null);
                    yield break;
                }
                var tex = DownloadHandlerTexture.GetContent(req);
                tex.wrapMode = TextureWrapMode.Clamp;
                var sprite = ToSprite(tex);
                cache[path] = sprite;
                onDone(sprite);
            }
        }

        public static string ResolveUrl(string path)
        {
            if (path.Contains("://")) return path;
            string full = Path.IsPathRooted(path) ? path : Path.Combine(StreamingRoot, path);
            // Android 의 StreamingAssets 는 이미 jar:file:// URL 이다
            if (full.Contains("://")) return full;
            return new Uri(full).AbsoluteUri;
        }

        static Sprite ToSprite(Texture2D tex)
        {
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
            s.name = tex.name;
            return s;
        }
    }
}
