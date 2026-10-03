using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Switches every legacy UI Text in the game scenes to Baloo 2 and adds a dark outline + drop shadow
/// to light-colored text so it stays readable on bright panels.
/// </summary>
public static class BalooFontApplier
{
    private const string BoldPath = "Assets/Hyper_Casual_UI/Fonts/Baloo2-Bold.ttf";
    private const string ExtraBoldPath = "Assets/Hyper_Casual_UI/Fonts/Baloo2-ExtraBold.ttf";
    private const int TitleFontSize = 40;

    private static readonly Color OutlineColor = new Color(0.07f, 0.16f, 0.22f, 0.9f);
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);

    [MenuItem("Tools/BrainCube/Font/Apply Baloo 2 To Open Scenes")]
    private static void ApplyToOpenScenes()
    {
        if (!LoadFonts(out Font bold, out Font extraBold))
            return;

        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            int changed = ApplyToScene(scene, bold, extraBold);
            if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
            count += changed;
        }

        Debug.Log($"[BalooFontApplier] {count} text updated in open scenes. Save the scene(s) to keep the change (Ctrl+Z to undo).");
    }

    [MenuItem("Tools/BrainCube/Font/Apply Baloo 2 To Build Scenes")]
    private static void ApplyToBuildScenes()
    {
        if (!LoadFonts(out Font bold, out Font extraBold))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        int count = 0;

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled) continue;

            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            int changed = ApplyToScene(scene, bold, extraBold);
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[BalooFontApplier] {buildScene.path}: {changed} text updated.");
            count += changed;
        }

        if (previousSetup.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);

        Debug.Log($"[BalooFontApplier] Done. {count} text updated in build scenes.");
    }

    private static bool LoadFonts(out Font bold, out Font extraBold)
    {
        bold = AssetDatabase.LoadAssetAtPath<Font>(BoldPath);
        extraBold = AssetDatabase.LoadAssetAtPath<Font>(ExtraBoldPath);
        if (bold != null && extraBold != null)
            return true;

        EditorUtility.DisplayDialog("Baloo 2", $"Font not found:\n{BoldPath}\n{ExtraBoldPath}", "OK");
        return false;
    }

    private static int ApplyToScene(Scene scene, Font bold, Font extraBold)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                ApplyToText(text, bold, extraBold);
                count++;
            }
        }
        return count;
    }

    private static void ApplyToText(Text text, Font bold, Font extraBold)
    {
        Undo.RecordObject(text, "Apply Baloo 2");
        // Vertical overflow is left untouched: some labels (e.g. theme buttons) rely on truncation to stay hidden.
        text.font = text.fontSize >= TitleFontSize ? extraBold : bold;
        RecordPrefabOverride(text);

        if (!IsLightColor(text.color))
            return;

        float outline = Mathf.Clamp(text.fontSize * 0.05f, 1f, 3f);
        Outline outlineEffect = GetExact<Outline>(text.gameObject);
        if (outlineEffect == null)
            outlineEffect = Undo.AddComponent<Outline>(text.gameObject);
        else
            Undo.RecordObject(outlineEffect, "Apply Baloo 2");
        outlineEffect.effectColor = OutlineColor;
        outlineEffect.effectDistance = new Vector2(outline, -outline);
        outlineEffect.useGraphicAlpha = true;
        RecordPrefabOverride(outlineEffect);

        float drop = Mathf.Clamp(text.fontSize * 0.08f, 1.5f, 4f);
        Shadow shadowEffect = GetExact<Shadow>(text.gameObject);
        if (shadowEffect == null)
            shadowEffect = Undo.AddComponent<Shadow>(text.gameObject);
        else
            Undo.RecordObject(shadowEffect, "Apply Baloo 2");
        shadowEffect.effectColor = ShadowColor;
        shadowEffect.effectDistance = new Vector2(0f, -drop);
        shadowEffect.useGraphicAlpha = true;
        RecordPrefabOverride(shadowEffect);
    }

    // Outline derives from Shadow, so GetComponent<Shadow> alone cannot tell them apart.
    private static T GetExact<T>(GameObject go) where T : Component
    {
        foreach (T component in go.GetComponents<T>())
        {
            if (component.GetType() == typeof(T))
                return component;
        }
        return null;
    }

    private static bool IsLightColor(Color color)
    {
        return color.r * 0.299f + color.g * 0.587f + color.b * 0.114f > 0.55f;
    }

    private static void RecordPrefabOverride(Object target)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
