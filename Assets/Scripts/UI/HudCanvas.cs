using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves the gameplay HUD canvas. A plain FindObjectOfType&lt;Canvas&gt; can return
/// DontDestroyOnLoad overlays such as the tutorial canvas.
/// </summary>
public static class HudCanvas
{
    public static Canvas Find()
    {
        if (GameUIController.Instance != null)
        {
            Canvas own = GameUIController.Instance.GetComponent<Canvas>();
            if (own != null)
                return own.rootCanvas;
        }

        Scene active = SceneManager.GetActiveScene();
        foreach (GameObject root in active.GetRootGameObjects())
        {
            Canvas canvas = root.GetComponentInChildren<Canvas>();
            if (canvas != null)
                return canvas.rootCanvas;
        }

        return null;
    }
}
