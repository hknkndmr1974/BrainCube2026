using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-created UI text uses the same font as the scene UI (set in the editor),
/// falling back to Unity's built-in font.
/// </summary>
public static class GameFont
{
    public static Font Resolve()
    {
        foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include))
        {
            if (text.font == null) continue;
            if (text.gameObject.scene.name == "DontDestroyOnLoad") continue;
            string name = text.font.name;
            if (name == "LegacyRuntime" || name == "Arial") continue;
            return text.font;
        }

        return Builtin();
    }

    public static Font Builtin()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
