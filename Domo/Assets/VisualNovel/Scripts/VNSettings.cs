using System;
using System.IO;
using UnityEngine;

namespace VisualNovel
{
    /// <summary>
    /// 시작 설정 화면에서 입력하는 값. 변경 즉시 자동 저장된다.
    ///  - 빌드: VN/settings.json (게임 폴더와 함께 옮길 수 있음)
    ///  - 에디터: persistentDataPath/vn_settings.json
    /// </summary>
    [Serializable]
    public class VNSettings
    {
        public enum DisplayMode { FullBody = 0, Portrait = 1 }

        [Header("캐릭터")]
        public string charAName = "유나";
        public string charAImage = "yuna.png";
        public string charBName = "하루";
        public string charBImage = "haru.png";

        [Header("캐릭터별 표시 방식")]
        [Tooltip("A: 전신 / 초상화")]
        public DisplayMode charAMode = DisplayMode.FullBody;
        [Tooltip("A 초상화 자르기 비율 0: 4:3, 1: 1:1, 2: 3:4 (가로:세로)")]
        public int charARatio = 0;
        [Tooltip("A 초상화로 자를 세로 위치 (0 = 맨 위, 1 = 맨 아래)")]
        [Range(0f, 1f)] public float charAFocus = 0f;
        public DisplayMode charBMode = DisplayMode.FullBody;
        public int charBRatio = 0;
        [Range(0f, 1f)] public float charBFocus = 0f;

        [Header("대사")]
        [Tooltip("직접 첨부한 대사 파일 (전체 경로). 있으면 폴더 선택보다 우선")]
        public string attachedScript = "";
        [Tooltip("VN/대사 폴더에서 고른 파일 이름")]
        public string folderScript = "";

        [Header("배경음")]
        [Tooltip("false 면 배경음 \"없음\" (폴더에 파일이 있어도 재생 안 함)")]
        public bool bgmEnabled = true;
        [Tooltip("VN/배경음 폴더에서 고른 파일 이름 (비우면 첫 번째 파일)")]
        public string bgmFile = "";
        [Range(0f, 1f)] public float bgmVolume = 0.6f;

        [Header("화면")]
        [Range(0.5f, 1.5f)] public float characterScale = 1f;
        [Tooltip("0 느리게, 1 보통, 2 빠르게, 3 즉시")]
        public int textSpeed = 1;
        public bool fullscreen = false;

        public static readonly string[] RatioLabels = { "4:3", "1:1", "3:4" };
        public static float AspectOf(int ratio) => ratio == 1 ? 1f : ratio == 2 ? 3f / 4f : 4f / 3f;

        // idx 0 = A, 1 = B
        public DisplayMode GetMode(int idx) => idx == 0 ? charAMode : charBMode;
        public int GetRatio(int idx) => idx == 0 ? charARatio : charBRatio;
        public float GetFocus(int idx) => idx == 0 ? charAFocus : charBFocus;
        public void SetMode(int idx, DisplayMode v) { if (idx == 0) charAMode = v; else charBMode = v; }
        public void SetRatio(int idx, int v) { if (idx == 0) charARatio = v; else charBRatio = v; }
        public void SetFocus(int idx, float v) { if (idx == 0) charAFocus = v; else charBFocus = v; }
        public float CharsPerSecond => textSpeed == 0 ? 20f : textSpeed == 2 ? 70f : textSpeed == 3 ? 0f : 40f;

        // ───────── 저장 / 불러오기 ─────────

        public static string FilePath =>
            Application.isEditor || !VNContent.IsExternal
                ? Path.Combine(Application.persistentDataPath, "vn_settings.json")
                : Path.Combine(VNContent.Root, "settings.json");

        public static VNSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<VNSettings>(File.ReadAllText(FilePath)) ?? new VNSettings();
            }
            catch (Exception e) { Debug.LogWarning($"[VN] 설정 파일을 읽지 못해 기본값을 사용합니다: {e.Message}"); }
            return new VNSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e) { Debug.LogWarning($"[VN] 설정 저장 실패: {e.Message}"); }
        }

        /// <summary>실제로 사용할 대사 파일의 전체 경로 (없으면 null)</summary>
        public string ResolveScriptPath()
        {
            if (!string.IsNullOrWhiteSpace(attachedScript))
            {
                var p = VNContent.ResolveFile(attachedScript.Trim(), VNContent.ScriptFolder);
                if (p != null) return p;
            }
            var files = VNContent.ListFiles(VNContent.ScriptFolder, VNContent.ScriptExtensions);
            if (files.Length == 0) return null;
            foreach (var f in files)
                if (Path.GetFileName(f) == folderScript) return f;
            return files[0];
        }

        /// <summary>실제로 재생할 배경음 파일의 전체 경로 ("없음" 이거나 파일이 없으면 null)</summary>
        public string ResolveBgmPath()
        {
            if (!bgmEnabled) return null;
            var files = VNContent.ListFiles(VNContent.BgmFolder, VNContent.AudioExtensions);
            if (files.Length == 0) return null;
            foreach (var f in files)
                if (Path.GetFileName(f) == bgmFile) return f;
            return files[0];
        }
    }
}
