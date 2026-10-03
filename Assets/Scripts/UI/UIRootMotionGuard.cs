using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The Unity UI Samples "SF Button" prefab ships with Apply Root Motion enabled. Under a panel whose
/// scale animates toward zero, root motion cannot resolve positions and collapses the buttons to the
/// panel center. UI animators never need root motion, so it is switched off on every scene load.
/// </summary>
public static class UIRootMotionGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.applyRootMotion && animator.GetComponentInParent<Canvas>(true) != null)
                    animator.applyRootMotion = false;
            }
        }
    }
}
