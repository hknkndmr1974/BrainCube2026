using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Editörde herhangi bir sahne açıldığında EventSystem nesnelerini denetler.
/// Eski StandaloneInputModule varsa, hata vermemesi için onu otomatik olarak
/// yeni InputSystemUIInputModule ile değiştirir.
/// </summary>
[InitializeOnLoad]
public static class InputModuleFixer
{
    static InputModuleFixer()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        FixEventSystemInActiveScene();
    }

    [MenuItem("Tools/Fix EventSystem Input Modules")]
    public static void FixEventSystemInActiveScene()
    {
        var eventSystems = Object.FindObjectsOfType<EventSystem>(true);
        if (eventSystems.Length == 0) return;

        System.Type inputModuleType =
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");

        if (inputModuleType == null) return;

        int fixedCount = 0;
        foreach (var es in eventSystems)
        {
            var standalone = es.GetComponent<StandaloneInputModule>();
            var touch = es.GetComponent<TouchInputModule>();

            if (standalone != null || touch != null)
            {
                if (standalone != null) Object.DestroyImmediate(standalone);
                if (touch != null) Object.DestroyImmediate(touch);

                if (es.GetComponent(inputModuleType) == null)
                {
                    es.gameObject.AddComponent(inputModuleType);
                }
                fixedCount++;
                
                // Sahneyi kirli (dirty) olarak işaretle ki kaydedilebilsin
                EditorUtility.SetDirty(es.gameObject);
                EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"[InputModuleFixer] {fixedCount} adet EventSystem nesnesi yeni Input System modülüne başarıyla dönüştürüldü.");
        }
    }
}
