using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VisualNovel
{
    /// <summary>
    /// "찾아보기" 파일 선택 창.
    ///  - 에디터: EditorUtility.OpenFilePanel
    ///  - Windows 빌드: Win32 GetOpenFileName (comdlg32)
    ///  - 그 외: 지원 안 함 (null)
    /// </summary>
    public static class VNFileDialog
    {
        public static bool IsSupported =>
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            true;
#else
            false;
#endif

        /// <param name="title">창 제목</param>
        /// <param name="filterName">예: "텍스트 파일"</param>
        /// <param name="extensions">예: { "txt" }</param>
        /// <param name="initialDir">처음 열릴 폴더</param>
        /// <returns>선택한 파일의 전체 경로, 취소하면 null</returns>
        public static string OpenFile(string title, string filterName, string[] extensions, string initialDir)
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(title, initialDir ?? "",
                new[] { filterName, string.Join(",", extensions), "모든 파일", "*" });
            return string.IsNullOrEmpty(path) ? null : path.Replace('/', '\\');
#elif UNITY_STANDALONE_WIN
            try
            {
                var patterns = string.Join(";", Array.ConvertAll(extensions, e => "*." + e));
                var ofn = new OpenFileName();
                ofn.lStructSize = Marshal.SizeOf(ofn);
                ofn.hwndOwner = GetActiveWindow();
                ofn.lpstrFilter = $"{filterName} ({patterns})\0{patterns}\0모든 파일 (*.*)\0*.*\0\0";
                ofn.nFilterIndex = 1;
                ofn.lpstrFile = new string('\0', 2048);
                ofn.nMaxFile = ofn.lpstrFile.Length;
                ofn.lpstrFileTitle = new string('\0', 512);
                ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
                ofn.lpstrInitialDir = initialDir;
                ofn.lpstrTitle = title;
                ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
                if (!GetOpenFileName(ofn)) return null;
                var result = ofn.lpstrFile;
                int nul = result.IndexOf('\0');
                if (nul >= 0) result = result.Substring(0, nul);
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VN] 파일 선택 창을 열 수 없습니다: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        const int OFN_NOCHANGEDIR = 0x00000008;
        const int OFN_PATHMUSTEXIST = 0x00000800;
        const int OFN_FILEMUSTEXIST = 0x00001000;
        const int OFN_EXPLORER = 0x00080000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
#endif
    }
}
