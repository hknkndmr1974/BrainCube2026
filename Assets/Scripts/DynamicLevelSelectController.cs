using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level select driven by Resources/LevelsJSON.
/// Scene should contain templates (from Tools/BrainCube/Convert Level Select To Dynamic).
/// World buttons can be baked in Edit Mode; level buttons fill a shared panel at runtime.
/// </summary>
public class DynamicLevelSelectController : MonoBehaviour
{
    public PanelManager panelManager;
    public Transform worldListContent;
    public Transform levelPanelsContainer;
    public GameObject worldButtonTemplate;
    public GameObject levelPanelTemplate;
    public GameObject levelButtonTemplate;

    [Tooltip("If false, uses WorldBtn_* already in the scene (Edit Mode bake). If true, rebuilds them from JSON every Play.")]
    public bool rebuildWorldButtonsOnPlay = false;

    [Header("Progress / Lock")]
    [SerializeField] private Sprite lockIcon;
    [SerializeField] private Color lockedButtonColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color selectedLevelColor = new Color(0.35f, 0.92f, 1f, 1f);
    [SerializeField] private Color selectedWorldColor = new Color(0.45f, 0.95f, 1f, 1f);

    private readonly SortedDictionary<int, List<int>> levelsByWorld = new();
    private static readonly Regex LevelNamePattern = new(@"^w(?<world>\d+)l(?<level>\d+)$");
    private Animator sharedLevelPanelAnimator;
    private Transform sharedLevelGrid;
    private ScrollRect sharedLevelScrollRect;
    private int currentWorldIndex = -1;
    private int focusedLevelIndex = -1;
    private bool isBuilt;
    private Coroutine levelPanelAnimationRoutine;
    private static readonly int OpenParameterId = Animator.StringToHash("Open");
    private const string ClosedStateName = "Closed";
    private const string OpenStateName = "Open";
    private const string LockChildName = "LockOverlay";
    private const string PrefsWorld = "SavedWorld";
    private const string PrefsLevel = "SavedLevel";

    private void Awake()
    {
        LevelProgress.EnsureDefaults();
        ResolveLockIcon();

        if (!HasRequiredReferences() && !ConfigureFromExistingLayout())
        {
            Debug.LogError("[DynamicLevelSelect] Templates are not configured. Run Tools/BrainCube/Convert Level Select To Dynamic.");
            return;
        }

        if (!isBuilt)
        {
            Build();
        }
    }

    private void OnEnable()
    {
        if (isBuilt)
            FocusLastPlayedProgress(playAnimation: false);
    }

    private void ResolveLockIcon()
    {
        if (lockIcon != null) return;

        lockIcon = Resources.Load<Sprite>("UI/Icon_PictoIcon_Lock");
        if (lockIcon == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("UI/Icon_PictoIcon_Lock");
            if (sprites != null && sprites.Length > 0)
                lockIcon = sprites[0];
        }

#if UNITY_EDITOR
        if (lockIcon == null)
        {
            lockIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_256/Icon_PictoIcon_Lock.Png");
        }
#endif

        if (lockIcon == null)
            Debug.LogWarning("[DynamicLevelSelect] Lock sprite yüklenemedi. Resources/UI/Icon_PictoIcon_Lock Sprite olmalı.");
    }

    private void ApplyLockOverlay(Transform root, bool unlocked)
    {
        Transform existing = root.Find(LockChildName);
        if (unlocked)
        {
            if (existing != null)
                existing.gameObject.SetActive(false);
            return;
        }

        if (lockIcon == null)
            ResolveLockIcon();

        // Sprite yoksa beyaz kare gösterme
        if (lockIcon == null)
        {
            if (existing != null)
                existing.gameObject.SetActive(false);
            return;
        }

        Image lockImage;
        if (existing == null)
        {
            GameObject overlay = new GameObject(LockChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(root, false);
            RectTransform rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(36f, 36f);
            rt.anchoredPosition = Vector2.zero;
            lockImage = overlay.GetComponent<Image>();
            lockImage.raycastTarget = false;
            overlay.transform.SetAsLastSibling();
        }
        else
        {
            existing.gameObject.SetActive(true);
            existing.SetAsLastSibling();
            lockImage = existing.GetComponent<Image>();
            RectTransform rt = existing as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(36f, 36f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        if (lockImage != null)
        {
            lockImage.sprite = lockIcon;
            lockImage.preserveAspect = true;
            lockImage.color = Color.white;
            lockImage.enabled = true;
        }
    }

    private void Build()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("[DynamicLevelSelect] UI templates are not configured.");
            return;
        }

        DiscoverLevels();
        if (levelsByWorld.Count == 0)
        {
            Debug.LogError("[DynamicLevelSelect] No level JSON files were found in Resources/LevelsJSON.");
            return;
        }

        sharedLevelPanelAnimator = levelPanelTemplate.GetComponent<Animator>();
        sharedLevelGrid = FindLevelGrid(levelPanelTemplate.transform);
        sharedLevelScrollRect = levelPanelTemplate.GetComponentInChildren<ScrollRect>(true);

        if (rebuildWorldButtonsOnPlay)
        {
            RebuildWorldButtons();
        }
        else
        {
            WireExistingWorldButtons();
        }

        isBuilt = true;
        FocusLastPlayedProgress(playAnimation: false);
    }

    /// <summary>Son oynanan world/level panelini hazırlar; PanelManager aktifse açar.</summary>
    public void FocusLastPlayedProgress(bool playAnimation = true)
    {
        if (!isBuilt || levelsByWorld.Count == 0)
            return;

        GetLastPlayed(out int world, out int level);
        focusedLevelIndex = level;

        bool panelReady = panelManager != null && panelManager.isActiveAndEnabled;

        if (playAnimation && panelReady && currentWorldIndex >= 0 && currentWorldIndex != world)
        {
            SelectWorld(world);
        }
        else
        {
            PopulateLevelPanel(world);
            currentWorldIndex = world;
            if (panelManager != null && sharedLevelPanelAnimator != null)
                panelManager.initiallyOpen = sharedLevelPanelAnimator;

            // Level Select henüz kapalıysa OpenPanel çağırma (inactive coroutine hatası).
            // Panel açılınca PanelManager.OnEnable → initiallyOpen ile açılır.
            if (panelReady && sharedLevelPanelAnimator != null)
            {
                sharedLevelPanelAnimator.gameObject.SetActive(true);
                if (!sharedLevelPanelAnimator.GetBool(OpenParameterId))
                    sharedLevelPanelAnimator.SetBool(OpenParameterId, true);
                panelManager.OpenPanel(sharedLevelPanelAnimator);
            }
        }

        HighlightWorldButtons(world);
        ScrollLevelIntoView(level);
    }

    private void GetLastPlayed(out int world, out int level)
    {
        world = PlayerPrefs.GetInt(PrefsWorld, 1);
        level = PlayerPrefs.GetInt(PrefsLevel, 1);

        if (!levelsByWorld.ContainsKey(world))
        {
            foreach (int w in levelsByWorld.Keys)
            {
                world = w;
                break;
            }
        }

        List<int> levels = levelsByWorld[world];
        if (levels == null || levels.Count == 0)
        {
            level = 1;
            return;
        }

        if (!levels.Contains(level))
            level = levels[0];
    }

    private void RebuildWorldButtons()
    {
        ClearGeneratedWorldButtons();
        foreach (KeyValuePair<int, List<int>> world in levelsByWorld)
        {
            CreateWorldButton(world.Key);
        }
    }

    private void WireExistingWorldButtons()
    {
        bool anyWired = false;
        for (int i = 0; i < worldListContent.childCount; i++)
        {
            Transform child = worldListContent.GetChild(i);
            if (!TryParseWorldButtonName(child.name, out int worldIndex))
            {
                continue;
            }

            if (!levelsByWorld.ContainsKey(worldIndex))
            {
                child.gameObject.SetActive(false);
                continue;
            }

            child.gameObject.SetActive(true);
            WireWorldButton(child.gameObject, worldIndex);
            anyWired = true;
        }

        // Add any worlds present in JSON but missing from the scene.
        foreach (int worldIndex in levelsByWorld.Keys)
        {
            if (worldListContent.Find($"WorldBtn_{worldIndex}") == null)
            {
                CreateWorldButton(worldIndex);
                anyWired = true;
            }
        }

        if (!anyWired)
        {
            RebuildWorldButtons();
        }
    }

    private void CreateWorldButton(int worldIndex)
    {
        GameObject worldButton = Instantiate(worldButtonTemplate, worldListContent);
        worldButton.name = $"WorldBtn_{worldIndex}";
        worldButton.SetActive(true);
        SetButtonText(worldButton, $"WORLD {worldIndex}");
        WireWorldButton(worldButton, worldIndex);
    }

    private void WireWorldButton(GameObject worldButton, int worldIndex)
    {
        Button button = worldButton.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        // World her zaman tıklanabilir; kilit sadece level butonlarında
        ResetButtonClick(button);
        button.interactable = true;
        ClearLockOverlay(worldButton.transform);
        ResetCanvasGroupAlpha(worldButton);

        button.onClick.AddListener(() => SelectWorld(worldIndex));
    }

    private static void ClearLockOverlay(Transform root)
    {
        Transform existing = root.Find(LockChildName);
        if (existing != null)
            existing.gameObject.SetActive(false);
    }

    private static bool TryParseWorldButtonName(string name, out int worldIndex)
    {
        worldIndex = 0;
        const string prefix = "WorldBtn_";
        if (!name.StartsWith(prefix))
        {
            return false;
        }

        return int.TryParse(name.Substring(prefix.Length), out worldIndex);
    }

    public void SelectWorld(int worldIndex)
    {
        if (!isBuilt)
        {
            Build();
        }

        if (!levelsByWorld.ContainsKey(worldIndex))
        {
            Debug.LogWarning($"[DynamicLevelSelect] World {worldIndex} has no levels.");
            return;
        }

        bool refreshContent = currentWorldIndex != worldIndex;
        PlayLevelPanelAnimation(worldIndex, refreshContent);
    }

    private void PopulateLevelPanel(int worldIndex)
    {
        if (!levelsByWorld.TryGetValue(worldIndex, out List<int> levels))
        {
            return;
        }

        GetLastPlayed(out int focusWorld, out int focusLevel);

        if (sharedLevelPanelAnimator == null)
        {
            sharedLevelPanelAnimator = levelPanelTemplate.GetComponent<Animator>();
            if (sharedLevelPanelAnimator == null)
            {
                Debug.LogError("[DynamicLevelSelect] Level panel template has no Animator.");
                return;
            }
        }

        levelPanelTemplate.name = "LevelPanelTemplate";
        Transform title = levelPanelTemplate.transform.Find("Panel/Title/TitleLabel");
        if (title != null && title.TryGetComponent(out Text titleText))
        {
            titleText.text = GetWorldDisplayName(worldIndex);
        }

        if (sharedLevelGrid == null)
        {
            sharedLevelGrid = FindLevelGrid(levelPanelTemplate.transform);
        }

        if (sharedLevelGrid == null)
        {
            Debug.LogError("[DynamicLevelSelect] Level panel template has no LevelGrid.");
            return;
        }

        ClearGeneratedLevelButtons();

        foreach (int levelIndex in levels)
        {
            GameObject levelButton = Instantiate(levelButtonTemplate, sharedLevelGrid);
            levelButton.name = $"LevelButton_{levelIndex}";
            levelButton.SetActive(true);
            SetButtonText(levelButton, levelIndex.ToString());

            Animator buttonAnimator = levelButton.GetComponent<Animator>();
            if (buttonAnimator != null)
            {
                buttonAnimator.applyRootMotion = false;
            }

            LevelSelectButton legacySelect = levelButton.GetComponent<LevelSelectButton>();
            if (legacySelect != null)
            {
                DestroyImmediate(legacySelect);
            }

            Button button = levelButton.GetComponent<Button>();
            bool unlocked = LevelProgress.IsLevelUnlocked(worldIndex, levelIndex);
            if (button != null)
            {
                int selectedLevel = levelIndex;
                ResetButtonClick(button);
                button.interactable = unlocked;
                if (unlocked)
                    button.onClick.AddListener(() => LoadLevel(worldIndex, selectedLevel));
            }

            ApplyLevelLockVisual(levelButton, unlocked);
            ApplyLevelButtonStars(levelButton, worldIndex, levelIndex);
            ApplyLevelSelectedVisual(
                levelButton,
                unlocked && worldIndex == focusWorld && levelIndex == focusLevel);
        }

        if (worldIndex == focusWorld)
            focusedLevelIndex = focusLevel;

        RefreshLevelGridLayout();
        if (worldIndex == focusWorld)
            ScrollLevelIntoView(focusLevel);
    }

    private void ApplyLevelSelectedVisual(GameObject levelButton, bool selected)
    {
        if (levelButton == null) return;

        Button btn = levelButton.GetComponent<Button>();
        bool unlocked = btn != null && btn.interactable;
        if (!unlocked)
            return;

        // Rengi bozma — sadece hafif ölçek ile seçili göster
        levelButton.transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
    }

    private void HighlightWorldButtons(int selectedWorld)
    {
        if (worldListContent == null) return;

        for (int i = 0; i < worldListContent.childCount; i++)
        {
            Transform child = worldListContent.GetChild(i);
            if (child.gameObject == worldButtonTemplate) continue;
            if (!TryParseWorldButtonName(child.name, out int worldIndex)) continue;

            child.localScale = worldIndex == selectedWorld ? Vector3.one * 1.05f : Vector3.one;
        }
    }

    private void ScrollLevelIntoView(int levelIndex)
    {
        if (sharedLevelScrollRect == null || sharedLevelGrid == null || levelIndex < 1)
            return;

        Transform target = sharedLevelGrid.Find($"LevelButton_{levelIndex}");
        if (target == null) return;

        Canvas.ForceUpdateCanvases();
        RectTransform content = sharedLevelScrollRect.content;
        RectTransform viewport = sharedLevelScrollRect.viewport != null
            ? sharedLevelScrollRect.viewport
            : sharedLevelScrollRect.GetComponent<RectTransform>();
        RectTransform item = target as RectTransform;
        if (content == null || viewport == null || item == null) return;

        // Dikey scroll varsayımı (grid)
        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        if (contentHeight <= viewportHeight)
        {
            sharedLevelScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        Vector3 itemLocal = content.InverseTransformPoint(item.position);
        float normalized = 1f - Mathf.Clamp01((-itemLocal.y) / Mathf.Max(0.01f, contentHeight - viewportHeight));
        sharedLevelScrollRect.verticalNormalizedPosition = normalized;
    }

    private void ApplyLevelLockVisual(GameObject levelButton, bool unlocked)
    {
        ApplyLockOverlay(levelButton.transform, unlocked);

        // Şablon CanvasGroup alpha=0 ile kopyalanabiliyor; açık seviyeleri soluk bırakma
        foreach (CanvasGroup cg in levelButton.GetComponentsInChildren<CanvasGroup>(true))
        {
            cg.alpha = unlocked ? 1f : 0.75f;
            if (cg.gameObject == levelButton)
            {
                cg.interactable = unlocked;
                cg.blocksRaycasts = unlocked;
            }
        }

        if (!unlocked && levelButton.GetComponent<CanvasGroup>() == null)
        {
            CanvasGroup rootCg = levelButton.AddComponent<CanvasGroup>();
            rootCg.alpha = 0.75f;
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;
        }

        // Image.color'a dokunma — orijinal SF UI görünümü kalsın
    }

    private static void ResetCanvasGroupAlpha(GameObject go)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) return;
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    private static void ApplyLevelButtonStars(GameObject levelButton, int worldIndex, int levelIndex)
    {
        Transform starsRoot = levelButton.transform.Find("Stars");
        if (starsRoot == null)
        {
            starsRoot = levelButton.transform.Find("Background/Stars");
        }
        if (starsRoot == null)
        {
            return;
        }

        if (!LevelProgress.IsLevelUnlocked(worldIndex, levelIndex) ||
            !LevelStarRating.HasPlayedLevel(worldIndex, levelIndex))
        {
            starsRoot.gameObject.SetActive(false);
            return;
        }

        starsRoot.gameObject.SetActive(true);
        int earnedStars = LevelStarRating.GetBestStars(worldIndex, levelIndex);

        for (int i = 0; i < starsRoot.childCount; i++)
        {
            Transform star = starsRoot.GetChild(i);
            bool filled = i < earnedStars;

            Transform empty = star.Find("Empty star");
            Transform full = star.Find("Full star");

            if (empty != null)
            {
                empty.gameObject.SetActive(!filled);
            }

            if (full != null)
            {
                full.gameObject.SetActive(filled);
            }
        }
    }

    private void PlayLevelPanelAnimation(int worldIndex, bool refreshContent)
    {
        if (sharedLevelPanelAnimator == null)
        {
            return;
        }

        if (levelPanelAnimationRoutine != null)
        {
            StopCoroutine(levelPanelAnimationRoutine);
        }

        levelPanelAnimationRoutine = StartCoroutine(PlayLevelPanelAnimationRoutine(worldIndex, refreshContent));
    }

    private IEnumerator PlayLevelPanelAnimationRoutine(int worldIndex, bool refreshContent)
    {
        Animator anim = sharedLevelPanelAnimator;
        anim.gameObject.SetActive(true);
        anim.transform.SetAsLastSibling();

        if (IsPanelFacingCamera(anim))
        {
            anim.SetBool(OpenParameterId, false);
            panelManager.CloseCurrent();
            yield return WaitForAnimatorState(anim, ClosedStateName);
            anim.gameObject.SetActive(true);
        }

        if (refreshContent)
        {
            PopulateLevelPanel(worldIndex);
            currentWorldIndex = worldIndex;
            HighlightWorldButtons(worldIndex);
            yield return null;
        }

        panelManager.OpenPanel(anim);
        if (!anim.GetBool(OpenParameterId))
        {
            anim.SetBool(OpenParameterId, true);
        }

        levelPanelAnimationRoutine = null;
    }

    private static bool IsPanelFacingCamera(Animator anim)
    {
        if (!anim.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (anim.GetBool(OpenParameterId))
        {
            return true;
        }

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        return state.IsName(OpenStateName) || state.IsName("Closing");
    }

    private static IEnumerator WaitForAnimatorState(Animator anim, string stateName)
    {
        yield return null;

        float timeout = 2f;
        float elapsed = 0f;
        while (anim != null && anim.isActiveAndEnabled && elapsed < timeout)
        {
            if (!anim.IsInTransition(0) && anim.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ClearGeneratedLevelButtons()
    {
        for (int i = sharedLevelGrid.childCount - 1; i >= 0; i--)
        {
            Transform child = sharedLevelGrid.GetChild(i);
            if (child.gameObject != levelButtonTemplate)
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void LoadLevel(int worldIndex, int levelIndex)
    {
        if (!LevelProgress.IsLevelUnlocked(worldIndex, levelIndex))
        {
            Debug.LogWarning($"[DynamicLevelSelect] Locked level blocked: w{worldIndex}l{levelIndex}");
            return;
        }

        LevelLoader.SelectedWorld = worldIndex;
        LevelLoader.SelectedLevel = levelIndex;
        SceneManager.LoadScene("BrainCube");
    }

    private void DiscoverLevels()
    {
        levelsByWorld.Clear();
        TextAsset[] levelFiles = Resources.LoadAll<TextAsset>("LevelsJSON");
        foreach (TextAsset levelFile in levelFiles)
        {
            Match match = LevelNamePattern.Match(levelFile.name);
            if (!match.Success)
            {
                continue;
            }

            int worldIndex = int.Parse(match.Groups["world"].Value);
            int levelIndex = int.Parse(match.Groups["level"].Value);
            if (!levelsByWorld.TryGetValue(worldIndex, out List<int> levels))
            {
                levels = new List<int>();
                levelsByWorld.Add(worldIndex, levels);
            }

            levels.Add(levelIndex);
        }

        foreach (List<int> levels in levelsByWorld.Values)
        {
            levels.Sort();
        }
    }

    private void ClearGeneratedWorldButtons()
    {
        for (int i = worldListContent.childCount - 1; i >= 0; i--)
        {
            Transform child = worldListContent.GetChild(i);
            if (child.gameObject != worldButtonTemplate)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private bool HasRequiredReferences()
    {
        return panelManager != null &&
               worldListContent != null &&
               levelPanelsContainer != null &&
               worldButtonTemplate != null &&
               levelPanelTemplate != null &&
               levelButtonTemplate != null;
    }

    private bool ConfigureFromExistingLayout()
    {
        Transform levelSelect = transform.Find("LevelSelect");
        if (levelSelect == null)
        {
            return false;
        }

        panelManager = levelSelect.GetComponentInChildren<PanelManager>(true);
        worldListContent = levelSelect.Find("WorldWindow/WorldScroll/Content")
            ?? levelSelect.Find("MenuFitRoot/WorldWindow/WorldScroll/Content");
        levelPanelsContainer = levelSelect.Find("LevelPanelsContainer")
            ?? levelSelect.Find("MenuFitRoot/LevelPanelsContainer");

        Transform templates = levelSelect.Find("LevelSelectTemplates");
        if (templates != null)
        {
            Transform worldTpl = templates.Find("WorldButtonTemplate");
            Transform levelTpl = templates.Find("LevelButtonTemplate");
            if (worldTpl != null) worldButtonTemplate = worldTpl.gameObject;
            if (levelTpl != null) levelButtonTemplate = levelTpl.gameObject;
        }

        if (levelPanelsContainer != null)
        {
            Transform panelTpl = levelPanelsContainer.Find("LevelPanelTemplate");
            if (panelTpl == null && levelPanelsContainer.childCount > 0)
            {
                panelTpl = levelPanelsContainer.GetChild(0);
            }

            if (panelTpl != null)
            {
                levelPanelTemplate = panelTpl.gameObject;
            }
        }

        return HasRequiredReferences();
    }

    private static Transform FindLevelGrid(Transform panel)
    {
        Transform grid = panel.Find("Panel/LevelScroll/LevelGrid");
        return grid != null ? grid : panel.Find("Panel/LevelGrid");
    }

    private void RefreshLevelGridLayout()
    {
        RectTransform gridRect = sharedLevelGrid as RectTransform;
        if (gridRect == null)
        {
            return;
        }

        // Stretch anchors: Left=20, Right=20, Top=180, Bottom=80
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.offsetMin = new Vector2(20f, 80f);
        gridRect.offsetMax = new Vector2(-20f, -180f);
        gridRect.localScale = new Vector3(1.1f, 1.1f, 1f);

        GridLayoutGroup grid = sharedLevelGrid.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(50f, 50f);
            grid.spacing = new Vector2(12f, 12f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
        Canvas.ForceUpdateCanvases();

        if (sharedLevelScrollRect != null)
        {
            sharedLevelScrollRect.StopMovement();
            sharedLevelScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private static void SetButtonText(GameObject button, string value)
    {
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = value;
        }
    }

    private string GetWorldDisplayName(int worldIndex)
    {
        if (worldListContent != null)
        {
            Transform worldButton = worldListContent.Find($"WorldBtn_{worldIndex}");
            if (worldButton != null)
            {
                Transform label = worldButton.Find("Background/Label") ?? worldButton.Find("Label");
                Text labelText = label != null
                    ? label.GetComponent<Text>()
                    : worldButton.GetComponentInChildren<Text>(true);

                if (labelText != null && !string.IsNullOrWhiteSpace(labelText.text))
                {
                    return labelText.text.Trim();
                }
            }
        }

        return $"WORLD {worldIndex}";
    }

    private static void ResetButtonClick(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.onClick = new Button.ButtonClickedEvent();
    }
}
