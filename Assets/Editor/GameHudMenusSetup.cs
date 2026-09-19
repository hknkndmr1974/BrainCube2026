using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds SF UI Pause + Level Complete overlays inside BrainCube.
/// Menu: Tools → BrainCube → Setup Game Pause And Level Complete Menus
/// </summary>
public static class GameHudMenusSetup
{
    private const string ScenePath = "Assets/BrainCube.unity";
    private const string SfButtonPath = "Assets/Unity UI Samples/Prefabs/SF Button.prefab";
    private const string SfTitlePath = "Assets/Unity UI Samples/Prefabs/SF Title.prefab";
    private const string PanelControllerPath = "Assets/Animation/GameHud/GameHudPanel.controller";
    private const string WindowSpritePath = "Assets/Unity UI Samples/Textures and Sprites/SF UI/SF Window.psd";
    private static readonly Vector2 WindowSize = new Vector2(720f, 480f);

    [MenuItem("Tools/BrainCube/Setup Game Pause And Level Complete Menus")]
    public static void SetupFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Setup", "Play Mode kapalıyken çalıştır.", "OK");
            return;
        }

        Setup();
        EditorUtility.DisplayDialog("Setup", "Pause ve Level Complete menüleri BrainCube sahnesine eklendi.", "OK");
    }

    public static void ConvertBatch()
    {
        Setup();
        EditorApplication.Exit(0);
    }

    private static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("BrainCube scene could not be opened.");
            return;
        }

        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        GameUIController controller = canvasGo.GetComponent<GameUIController>();
        if (controller == null)
        {
            controller = canvasGo.AddComponent<GameUIController>();
        }

        GameObject pauseMenu = EnsurePanel(canvasGo.transform, "PauseMenu", "PAUSE",
            new[]
            {
                ("ResumeBtn", "RESUME"),
                ("RestartBtn", "RESTART"),
                ("MainMenuBtn", "MAIN MENU")
            },
            new[] { 40f, -50f, -140f });

        GameObject levelCompleteMenu = EnsurePanel(canvasGo.transform, "LevelCompleteMenu", "LEVEL COMPLETE",
            new[]
            {
                ("NextLevelBtn", "NEXT LEVEL"),
                ("RestartBtn", "RESTART"),
                ("MainMenuBtn", "MAIN MENU")
            },
            new[] { 40f, -50f, -140f });

        Button pauseHud = EnsurePauseHudButton(canvasGo.transform, controller);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("pauseRoot").objectReferenceValue = pauseMenu;
        so.FindProperty("levelCompleteRoot").objectReferenceValue = levelCompleteMenu;
        so.FindProperty("pauseAnimator").objectReferenceValue = pauseMenu.GetComponent<Animator>();
        so.FindProperty("levelCompleteAnimator").objectReferenceValue = levelCompleteMenu.GetComponent<Animator>();
        so.FindProperty("pauseHudButton").objectReferenceValue = pauseHud;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        pauseMenu.SetActive(false);
        levelCompleteMenu.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GameHudMenusSetup] Pause + Level Complete menus ready.");
    }

    private static GameObject EnsurePanel(
        Transform canvas,
        string panelName,
        string titleText,
        (string name, string label)[] buttons,
        float[] buttonYs)
    {
        Transform existing = canvas.Find(panelName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(Animator));
        panel.transform.SetParent(canvas, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        StretchFull(panelRt);

        Animator animator = panel.GetComponent<Animator>();
        RuntimeAnimatorController panelController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PanelControllerPath);
        if (panelController != null)
        {
            animator.runtimeAnimatorController = panelController;
        }

        animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // Dim background
        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dim.transform.SetParent(panel.transform, false);
        StretchFull(dim.GetComponent<RectTransform>());
        Image dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = true;

        // Window
        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        window.transform.SetParent(panel.transform, false);
        RectTransform windowRt = window.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = WindowSize;
        windowRt.anchoredPosition = new Vector2(0f, 700f);

        Image windowImage = window.GetComponent<Image>();
        Sprite windowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WindowSpritePath);
        if (windowSprite != null)
        {
            windowImage.sprite = windowSprite;
            windowImage.type = Image.Type.Sliced;
        }

        windowImage.color = Color.white;

        // Title
        GameObject titlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SfTitlePath);
        GameObject titleGo;
        if (titlePrefab != null)
        {
            titleGo = (GameObject)PrefabUtility.InstantiatePrefab(titlePrefab, window.transform);
        }
        else
        {
            titleGo = new GameObject("SF Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            titleGo.transform.SetParent(window.transform, false);
        }

        titleGo.name = "SF Title";
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-80f, 90f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);

        Transform titleLabel = titleGo.transform.Find("TitleLabel");
        if (titleLabel == null)
        {
            foreach (Text text in titleGo.GetComponentsInChildren<Text>(true))
            {
                text.text = titleText;
                text.alignment = TextAnchor.MiddleCenter;
            }
        }
        else
        {
            Text text = titleLabel.GetComponent<Text>();
            if (text != null)
            {
                text.text = titleText;
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SfButtonPath);
        for (int i = 0; i < buttons.Length; i++)
        {
            CreateSfButton(window.transform, buttonPrefab, buttons[i].name, buttons[i].label, buttonYs[i]);
        }

        return panel;
    }

    private static void CreateSfButton(
        Transform parent,
        GameObject buttonPrefab,
        string objectName,
        string label,
        float anchoredY)
    {
        GameObject buttonGo;
        if (buttonPrefab != null)
        {
            buttonGo = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, parent);
        }
        else
        {
            buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);
        }

        buttonGo.name = objectName;
        RectTransform rt = buttonGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 70f);
        rt.anchoredPosition = new Vector2(0f, anchoredY);

        Transform labelTrans = buttonGo.transform.Find("Background/Label");
        if (labelTrans == null)
        {
            labelTrans = buttonGo.transform.Find("Label");
        }

        if (labelTrans != null)
        {
            Text text = labelTrans.GetComponent<Text>();
            if (text != null)
            {
                text.text = label;
                text.fontSize = 22;
                text.alignment = TextAnchor.MiddleCenter;
                EditorUtility.SetDirty(text);
            }
        }
    }

    private static Button EnsurePauseHudButton(Transform canvas, GameUIController controller)
    {
        Transform existingPause = canvas.Find("PauseButton");
        Transform existingMain = canvas.Find("MainMenuButton");

        GameObject btnGo;
        if (existingPause != null)
        {
            btnGo = existingPause.gameObject;
        }
        else if (existingMain != null)
        {
            existingMain.name = "PauseButton";
            btnGo = existingMain.gameObject;
        }
        else
        {
            GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SfButtonPath);
            if (buttonPrefab != null)
            {
                btnGo = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, canvas);
            }
            else
            {
                btnGo = new GameObject("PauseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(canvas, false);
            }

            btnGo.name = "PauseButton";
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(180f, 50f);
        }

        Transform labelTrans = btnGo.transform.Find("Background/Label") ?? btnGo.transform.Find("Label");
        if (labelTrans != null)
        {
            Text text = labelTrans.GetComponent<Text>();
            if (text != null)
            {
                text.text = "PAUSE";
                text.fontSize = 20;
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        Button button = btnGo.GetComponent<Button>();
        if (button != null)
        {
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            }

            UnityEventTools.AddVoidPersistentListener(button.onClick, controller.OpenPause);
        }

        return button;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
