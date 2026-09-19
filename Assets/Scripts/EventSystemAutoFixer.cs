using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public static class EventSystemAutoFixer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnLoad()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        FixEventSystem();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FixEventSystem();
    }

    private static void FixEventSystem()
    {
#if ENABLE_INPUT_SYSTEM
        EventSystem es = Object.FindObjectOfType<EventSystem>();
        if (es != null)
        {
            StandaloneInputModule sim = es.GetComponent<StandaloneInputModule>();
            TouchInputModule tim = es.GetComponent<TouchInputModule>();

            if (sim != null || tim != null)
            {
                if (sim != null) Object.DestroyImmediate(sim);
                if (tim != null) Object.DestroyImmediate(tim);

                // Add the new InputSystemUIInputModule dynamically via Reflection to prevent assembly errors
                System.Type uiModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (uiModuleType != null)
                {
                    es.gameObject.AddComponent(uiModuleType);
                    Debug.Log("<color=cyan>[EventSystemAutoFixer]</color> Eski input modülleri (Standalone/Touch) tespit edildi ve otomatik olarak yeni InputSystemUIInputModule ile değiştirildi!");
                }
            }
        }
#endif
    }
}
