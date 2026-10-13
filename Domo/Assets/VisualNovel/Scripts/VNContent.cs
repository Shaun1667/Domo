using System;
using System.IO;
using UnityEngine;

namespace VisualNovel
{
    /// <summary>
    /// 대본/이미지를 읽어올 콘텐츠 폴더를 결정한다.
    ///  - 빌드(PC): 실행 파일 옆의 VN 폴더. 없으면 내장된 StreamingAssets/VN 을 복사해 만들어 준다.
    ///  - 에디터 / 모바일 / WebGL: StreamingAssets/VN
    ///  - 실행 인자 -vnfolder &lt;경로&gt; 로 다른 폴더 지정 가능
    /// </summary>
    public static class VNContent
    {
        public static string Root { get; private set; }
        public static bool IsExternal { get; private set; }

        public const string ScriptFolder = "대사";
        public const string ImageFolder = "이미지";
        public const string BgmFolder = "배경음";
        public const string UIFolder = "UI";
        public static readonly string[] ScriptExtensions = { ".txt", ".json" };
        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };
        public static readonly string[] AudioExtensions = { ".mp3", ".ogg", ".wav" };

        public static string BuiltinRoot => Path.Combine(Application.streamingAssetsPath, "VN");

        /// <summary>실행 파일(.exe)이 있는 폴더</summary>
        public static string GameFolder
        {
            get
            {
                var data = Application.dataPath; // Windows/Linux: <폴더>/<이름>_Data, Mac: <이름>.app/Contents
                if (Application.platform == RuntimePlatform.OSXPlayer)
                    return Path.GetFullPath(Path.Combine(data, "..", ".."));
                return Path.GetDirectoryName(data);
            }
        }

        public static bool SupportsExternalFolder =>
            Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.LinuxPlayer;

        public static string Resolve(string folderName, bool useExternal)
        {
            IsExternal = false;
            Root = BuiltinRoot;

            var overridePath = GetArg("-vnfolder");
            if (!string.IsNullOrEmpty(overridePath))
            {
                Root = Path.GetFullPath(overridePath);
                IsExternal = true;
                return Root;
            }

            if (!useExternal || !SupportsExternalFolder) return Root;

            var external = Path.Combine(GameFolder, folderName);
            try
            {
                if (!Directory.Exists(external) && Directory.Exists(BuiltinRoot))
                {
                    CopyDirectory(BuiltinRoot, external);
                    Debug.Log($"[VN] 외부 콘텐츠 폴더 생성: {external}");
                }
                if (Directory.Exists(external))
                {
                    Root = external;
                    IsExternal = true;
                    AddMissingSubfolders();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VN] 외부 폴더를 사용할 수 없어 내장 데이터를 사용합니다: {e.Message}");
            }
            return Root;
        }

        /// <summary>이전 버전 빌드 폴더에 새 하위 폴더(대사/이미지/배경음)가 없으면 내장 데이터에서 채워 넣는다.</summary>
        static void AddMissingSubfolders()
        {
            foreach (var sub in new[] { ScriptFolder, ImageFolder, BgmFolder, UIFolder })
            {
                var dst = Path.Combine(Root, sub);
                if (Directory.Exists(dst)) continue;
                var src = Path.Combine(BuiltinRoot, sub);
                if (Directory.Exists(src)) CopyDirectory(src, dst);
                else Directory.CreateDirectory(dst);
            }
        }

        /// <summary>하위 폴더 경로 (없으면 null 이 아니라 경로만 반환)</summary>
        public static string Folder(string sub) => Path.Combine(Root ?? BuiltinRoot, sub);

        /// <summary>하위 폴더의 파일 목록 (이름순)</summary>
        public static string[] ListFiles(string sub, string[] extensions)
        {
            var dir = Folder(sub);
            if (dir.Contains("://") || !Directory.Exists(dir)) return new string[0];
            var list = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.GetFiles(dir))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (Array.IndexOf(extensions, ext) >= 0) list.Add(f);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list.ToArray();
        }

        /// <summary>
        /// 파일 경로 찾기: 절대경로 → 콘텐츠 폴더 기준 → 하위 폴더(sub) 기준 순서로 확인. 없으면 null.
        /// </summary>
        public static string ResolveFile(string path, string sub)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains("://")) return null;
            path = path.Trim().Trim('"');
            try
            {
                if (Path.IsPathRooted(path)) return File.Exists(path) ? path : null;
                var a = Path.Combine(Root ?? BuiltinRoot, path);
                if (File.Exists(a)) return a;
                if (sub != null)
                {
                    var b = Path.Combine(Folder(sub), path);
                    if (File.Exists(b)) return b;
                }
            }
            catch (ArgumentException) { }
            return null;
        }

        /// <summary>탐색기로 폴더 열기</summary>
        public static void OpenFolder(string sub)
        {
            var dir = Path.GetFullPath(sub == null ? (Root ?? BuiltinRoot) : Folder(sub));
            try { Directory.CreateDirectory(dir); } catch { }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            // 한글 경로를 URL 로 열면 퍼센트 인코딩 때문에 탐색기가 못 여는 경우가 있어 explorer.exe 를 직접 실행
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + dir.Replace('/', '\\') + "\"",
                    UseShellExecute = true,
                });
                return;
            }
            catch (Exception e) { Debug.LogWarning($"[VN] 탐색기 실행 실패, URL 로 다시 시도: {e.Message}"); }
#endif
            Application.OpenURL("file:///" + dir.Replace('\\', '/'));
        }

        /// <summary>폴더 안 파일들의 변경 여부를 판단하기 위한 값 (최신 수정 시각 + 파일 수)</summary>
        public static long Fingerprint()
        {
            if (string.IsNullOrEmpty(Root) || Root.Contains("://") || !Directory.Exists(Root)) return 0;
            long latest = 0, count = 0;
            foreach (var f in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (Array.IndexOf(ScriptExtensions, ext) < 0 && Array.IndexOf(ImageExtensions, ext) < 0) continue;
                if (Path.GetFileName(f) == "settings.json") continue;
                count++;
                long t = File.GetLastWriteTimeUtc(f).Ticks;
                if (t > latest) latest = t;
            }
            return latest ^ (count << 1);
        }

        public static void CopyDirectory(string src, string dst, bool overwrite = false)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                var target = Path.Combine(dst, Path.GetFileName(file));
                if (overwrite || !File.Exists(target)) File.Copy(file, target, overwrite);
            }
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)), overwrite);
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
