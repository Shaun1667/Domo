using UnityEditor;
using UnityEngine;

namespace VisualNovel.EditorTools
{
    static class VNMenu
    {
        [MenuItem("Tools/Visual Novel/Create VN Player")]
        [MenuItem("GameObject/UI/Visual Novel Player")]
        static void Create()
        {
            var go = new GameObject("VNPlayer", typeof(RectTransform));
            go.layer = 5;
            var player = go.AddComponent<VNPlayer>();
            VNUIBuilder.Build(player);
            Undo.RegisterCreatedObjectUndo(go, "Create VN Player");
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/Visual Novel/Open Script Folder (StreamingAssets/VN)")]
        static void OpenFolder()
        {
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "VN");
            System.IO.Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(System.IO.Path.Combine(path, "script.txt"));
        }
    }
}
