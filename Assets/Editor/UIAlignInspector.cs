using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class UIAlignInspector : EditorWindow
{
    [MenuItem("Tools/Inspect UI Alignments")]
    public static void InspectAlignments()
    {
        string scenePath = "Assets/Scenes/MainMenu.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("MainMenu scene could not be loaded!");
            return;
        }

        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("Canvas not found!");
            return;
        }

        Transform levelSelect = canvasGo.transform.Find("LevelSelect");
        if (levelSelect == null)
        {
            Debug.LogError("LevelSelect not found!");
            return;
        }

        PrintRectTransform(levelSelect.GetComponent<RectTransform>(), "LevelSelect (Main Panel)");

        Transform worldWindow = levelSelect.Find("WorldWindow")
            ?? levelSelect.Find("MenuFitRoot/WorldWindow");
        if (worldWindow != null)
        {
            PrintRectTransform(worldWindow.GetComponent<RectTransform>(), "WorldWindow (Left/Worlds Panel)");
        }

        Transform levelPanels = levelSelect.Find("LevelPanelsContainer")
            ?? levelSelect.Find("MenuFitRoot/LevelPanelsContainer");
        if (levelPanels != null)
        {
            PrintRectTransform(levelPanels.GetComponent<RectTransform>(), "LevelPanelsContainer (Right/Levels Panel)");
            
            // Bir tane örnek alt pencerenin panelini inceleyelim
            Transform firstSub = levelPanels.Find("World1_Levels");
            if (firstSub != null)
            {
                PrintRectTransform(firstSub.GetComponent<RectTransform>(), "World1_Levels (Sub-window)");
                Transform panel = firstSub.Find("Panel");
                if (panel != null)
                {
                    PrintRectTransform(panel.GetComponent<RectTransform>(), "World1_Levels/Panel");
                }
            }
        }
    }

    private static void PrintRectTransform(RectTransform rt, string label)
    {
        if (rt == null) return;
        Debug.Log($"<b>[{label}]</b>\n" +
                  $"  - AnchoredPosition: {rt.anchoredPosition}\n" +
                  $"  - SizeDelta: {rt.sizeDelta}\n" +
                  $"  - AnchorMin: {rt.anchorMin}\n" +
                  $"  - AnchorMax: {rt.anchorMax}\n" +
                  $"  - OffsetMin (Left, Bottom): {rt.offsetMin}\n" +
                  $"  - OffsetMax (Right, Top): {rt.offsetMax}\n" +
                  $"  - Pivot: {rt.pivot}\n");
    }
}
