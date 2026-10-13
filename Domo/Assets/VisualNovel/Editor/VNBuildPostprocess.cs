using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VisualNovel.EditorTools
{
    /// <summary>
    /// PC 빌드 후 실행 파일 옆에 VN 폴더(대본/이미지)를 복사한다.
    /// 이미 있는 파일은 덮어쓰지 않으므로 빌드 폴더에서 수정한 내용은 보존된다.
    /// </summary>
    class VNBuildPostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64 &&
                target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64)
                return;

            var src = Path.Combine(Application.dataPath, "StreamingAssets", "VN");
            if (!Directory.Exists(src)) return;

            var output = report.summary.outputPath; // Windows: ...\Game.exe, Mac: ...\Game.app
            var gameFolder = Path.GetDirectoryName(output);
            var dst = Path.Combine(gameFolder, "VN");
            VNContent.CopyDirectory(src, dst, overwrite: false);
            Debug.Log($"[VN] 빌드 폴더에 콘텐츠 복사: {dst}");
        }
    }
}
