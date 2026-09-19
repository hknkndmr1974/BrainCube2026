using UnityEditor;
using UnityEngine;

public static class AudioEventLibraryEditor
{
    private const string LibraryPath = "Assets/Resources/AudioEventLibrary.asset";

    [MenuItem("Tools/BrainCube/Select Audio Event Library")]
    private static void SelectLibrary()
    {
        AudioEventLibrary library = AssetDatabase.LoadAssetAtPath<AudioEventLibrary>(LibraryPath);
        if (library == null)
        {
            Debug.LogError($"Audio Event Library bulunamadı: {LibraryPath}");
            return;
        }

        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
    }
}
