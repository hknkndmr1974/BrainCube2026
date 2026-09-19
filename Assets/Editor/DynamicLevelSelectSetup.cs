#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Converts the old baked World1..World15 LevelSelect hierarchy into a
/// single shared panel + button templates, without resetting MainMenu layout.
/// </summary>
public static class DynamicLevelSelectSetup
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private static readonly Regex LevelNamePattern = new(@"^w(?<world>\d+)l(?<level>\d+)$");

    [MenuItem("Tools/BrainCube/Convert Level Select To Dynamic")]
    public static void ConvertFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Convert Level Select", "Play Mode kapalıyken çalıştır.", "OK");
            return;
        }

        if (!ConvertScene(true))
        {
            EditorUtility.DisplayDialog("Convert Level Select", "Dönüşüm başarısız. Console'a bak.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Convert Level Select",
                "Eski World1-15 panelleri kaldırıldı.\n" +
                "Tek LevelPanelTemplate + buton şablonları kuruldu.\n" +
                "World butonları JSON'dan Edit Mode'da üretildi.",
                "OK");
        }
    }

    /// <summary>Batchmode entry: Unity.exe -batchmode -executeMethod DynamicLevelSelectSetup.ConvertBatch</summary>
    public static void ConvertBatch()
    {
        bool ok = ConvertScene(false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool ConvertScene(bool interactive)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[DynamicLevelSelectSetup] Scene not found: {ScenePath}");
            return false;
        }

        GameObject canvas = FindInScene("Canvas");
        if (canvas == null)
        {
            Debug.LogError("[DynamicLevelSelectSetup] Canvas not found.");
            return false;
        }

        Transform levelSelect = canvas.transform.Find("LevelSelect");
        if (levelSelect == null)
        {
            Debug.LogError("[DynamicLevelSelectSetup] LevelSelect not found.");
            return false;
        }

        PanelManager panelManager = levelSelect.GetComponentInChildren<PanelManager>(true);
        Transform worldContent = levelSelect.Find("WorldWindow/WorldScroll/Content")
            ?? levelSelect.Find("MenuFitRoot/WorldWindow/WorldScroll/Content");
        Transform levelPanelsContainer = levelSelect.Find("LevelPanelsContainer")
            ?? levelSelect.Find("MenuFitRoot/LevelPanelsContainer");
        if (panelManager == null || worldContent == null || levelPanelsContainer == null)
        {
            Debug.LogError("[DynamicLevelSelectSetup] LevelSelect structure incomplete.");
            return false;
        }

        // --- Templates holder (inactive, not animated) ---
        Transform templates = levelSelect.Find("LevelSelectTemplates");
        if (templates == null)
        {
            var go = new GameObject("LevelSelectTemplates");
            templates = go.transform;
            templates.SetParent(levelSelect, false);
        }
        templates.gameObject.SetActive(false);

        // --- World button template ---
        GameObject worldButtonTemplate = EnsureWorldButtonTemplate(worldContent, templates);

        // --- Level panel template from first world panel ---
        GameObject levelPanelTemplate = EnsureLevelPanelTemplate(levelPanelsContainer, templates, out GameObject levelButtonTemplate);
        if (levelPanelTemplate == null || levelButtonTemplate == null)
        {
            Debug.LogError("[DynamicLevelSelectSetup] Could not create level panel/button templates.");
            return false;
        }

        Animator panelAnimator = levelPanelTemplate.GetComponent<Animator>();
        panelManager.initiallyOpen = panelAnimator;
        EditorUtility.SetDirty(panelManager);

        // --- Controller on Canvas ---
        DynamicLevelSelectController controller = canvas.GetComponent<DynamicLevelSelectController>();
        if (controller == null)
        {
            controller = canvas.AddComponent<DynamicLevelSelectController>();
        }

        controller.panelManager = panelManager;
        controller.worldListContent = worldContent;
        controller.levelPanelsContainer = levelPanelsContainer;
        controller.worldButtonTemplate = worldButtonTemplate;
        controller.levelPanelTemplate = levelPanelTemplate;
        controller.levelButtonTemplate = levelButtonTemplate;
        controller.rebuildWorldButtonsOnPlay = false;
        EditorUtility.SetDirty(controller);

        // --- Bake world buttons from JSON for Edit Mode visibility ---
        ClearChildrenExcept(worldContent, null);
        SortedDictionary<int, List<int>> levelsByWorld = DiscoverLevels();
        foreach (KeyValuePair<int, List<int>> world in levelsByWorld)
        {
            int worldIndex = world.Key;
            GameObject worldButton = Object.Instantiate(worldButtonTemplate, worldContent);
            worldButton.name = $"WorldBtn_{worldIndex}";
            worldButton.SetActive(true);
            SetButtonText(worldButton, $"WORLD {worldIndex}");
            ResetButtonClick(worldButton.GetComponent<Button>());
            // Runtime SelectWorld is wired in Awake; no persistent OpenPanel refs.
            EditorUtility.SetDirty(worldButton);
        }

        // Keep panel as inactive template in container (visible structure in hierarchy)
        levelPanelTemplate.SetActive(false);
        levelPanelTemplate.transform.SetParent(levelPanelsContainer, false);
        levelPanelTemplate.name = "LevelPanelTemplate";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DynamicLevelSelectSetup] Done. Worlds baked: {levelsByWorld.Count}");
        return true;
    }

    private static GameObject EnsureWorldButtonTemplate(Transform worldContent, Transform templates)
    {
        GameObject template = null;

        // Prefer existing WorldBtn_1 / first button as visual template
        for (int i = 0; i < worldContent.childCount; i++)
        {
            Transform child = worldContent.GetChild(i);
            if (child.GetComponent<Button>() != null)
            {
                template = child.gameObject;
                break;
            }
        }

        if (template == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Unity UI Samples/Prefabs/SF Button.prefab");
            template = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, templates)
                : new GameObject("WorldButtonTemplate", typeof(RectTransform), typeof(Button));
        }

        // Remove siblings
        for (int i = worldContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = worldContent.GetChild(i).gameObject;
            if (child != template)
            {
                Object.DestroyImmediate(child);
            }
        }

        template.name = "WorldButtonTemplate";
        template.SetActive(false);
        template.transform.SetParent(templates, false);
        ResetButtonClick(template.GetComponent<Button>());

        LayoutElement le = template.GetComponent<LayoutElement>();
        if (le == null) le = template.AddComponent<LayoutElement>();
        le.preferredHeight = 60f;

        return template;
    }

    private static GameObject EnsureLevelPanelTemplate(
        Transform levelPanelsContainer,
        Transform templates,
        out GameObject levelButtonTemplate)
    {
        levelButtonTemplate = null;
        GameObject panelTemplate = null;

        // Find World1_Levels or any World*_Levels / existing LevelPanelTemplate
        for (int i = 0; i < levelPanelsContainer.childCount; i++)
        {
            Transform child = levelPanelsContainer.GetChild(i);
            if (child.name == "LevelPanelTemplate" || child.name == "World1_Levels" || child.name.StartsWith("World"))
            {
                panelTemplate = child.gameObject;
                break;
            }
        }

        if (panelTemplate == null && levelPanelsContainer.childCount > 0)
        {
            panelTemplate = levelPanelsContainer.GetChild(0).gameObject;
        }

        if (panelTemplate == null)
        {
            return null;
        }

        // Destroy all other world panels
        for (int i = levelPanelsContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = levelPanelsContainer.GetChild(i).gameObject;
            if (child != panelTemplate)
            {
                Object.DestroyImmediate(child);
            }
        }

        panelTemplate.name = "LevelPanelTemplate";
        Transform grid = FindLevelGrid(panelTemplate.transform);
        if (grid == null)
        {
            Transform panel = panelTemplate.transform.Find("Panel");
            if (panel == null) return null;

            GameObject gridGo = new GameObject("LevelGrid", typeof(RectTransform));
            grid = gridGo.transform;
            grid.SetParent(panel, false);
            RectTransform gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero;
            gridRt.anchorMax = Vector2.one;
            gridRt.offsetMin = new Vector2(20, 12);
            gridRt.offsetMax = new Vector2(-20, -148);
            GridLayoutGroup glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(50, 50);
            glg.spacing = new Vector2(12, 12);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 5;
            glg.childAlignment = TextAnchor.UpperCenter;
        }

        // Keep one level button as template
        if (grid.childCount > 0)
        {
            levelButtonTemplate = grid.GetChild(0).gameObject;
        }
        else
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Unity UI Samples/Prefabs/SF Grid Button.prefab");
            levelButtonTemplate = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, grid)
                : new GameObject("LevelButtonTemplate", typeof(RectTransform), typeof(Button));
        }

        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            GameObject child = grid.GetChild(i).gameObject;
            if (child != levelButtonTemplate)
            {
                Object.DestroyImmediate(child);
            }
        }

        levelButtonTemplate.name = "LevelButtonTemplate";
        levelButtonTemplate.SetActive(false);
        levelButtonTemplate.transform.SetParent(templates, false);
        ResetButtonClick(levelButtonTemplate.GetComponent<Button>());
        LevelSelectButton legacy = levelButtonTemplate.GetComponent<LevelSelectButton>();
        if (legacy != null) Object.DestroyImmediate(legacy);
        Animator btnAnim = levelButtonTemplate.GetComponent<Animator>();
        if (btnAnim != null) btnAnim.applyRootMotion = false;

        return panelTemplate;
    }

    private static Transform FindLevelGrid(Transform panelRoot)
    {
        Transform grid = panelRoot.Find("Panel/LevelScroll/LevelGrid");
        return grid != null ? grid : panelRoot.Find("Panel/LevelGrid");
    }

    private static SortedDictionary<int, List<int>> DiscoverLevels()
    {
        var map = new SortedDictionary<int, List<int>>();
        string folder = Path.Combine(Application.dataPath, "Resources/LevelsJSON");
        if (!Directory.Exists(folder)) return map;

        foreach (string file in Directory.GetFiles(folder, "w*l*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            Match match = LevelNamePattern.Match(name);
            if (!match.Success) continue;

            int world = int.Parse(match.Groups["world"].Value);
            int level = int.Parse(match.Groups["level"].Value);
            if (!map.TryGetValue(world, out List<int> levels))
            {
                levels = new List<int>();
                map.Add(world, levels);
            }
            levels.Add(level);
        }

        foreach (List<int> levels in map.Values)
        {
            levels.Sort();
        }

        return map;
    }

    private static void ClearChildrenExcept(Transform parent, GameObject keep)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (child != keep) Object.DestroyImmediate(child);
        }
    }

    private static void ResetButtonClick(Button button)
    {
        if (button != null) button.onClick = new Button.ButtonClickedEvent();
    }

    private static void SetButtonText(GameObject button, string value)
    {
        Text text = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (text != null) text.text = value;
    }

    private static GameObject FindInScene(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name && go.scene.isLoaded) return go;
        }
        return null;
    }
}
#endif
