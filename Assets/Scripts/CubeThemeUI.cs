using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Theme paneli: durum yazısı, önizleme küpü, kategori sekmeleri (Wood/Metal/Wall).
/// Tema buton listesini üretmez — Edit Mode'da sen yerleştirdiğin
/// CubeThemeSelectButton'lar seçimi yapar. Sekmeler sadece ilgili butonları gösterir/gizler.
/// </summary>
public sealed class CubeThemeUI : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    [Header("Scene Layout")]
    [SerializeField] private RectTransform settingsRoot;
    [SerializeField] private Text statusLabel;
    [SerializeField] private RectTransform previewAnchor;
    [SerializeField] private Transform tabsRoot;
    [SerializeField] private Transform themesRoot;

    [Header("Preview")]
    [SerializeField] private Vector3 previewCubeLocalPosition = new Vector3(200f, -173f, -50f);
    [SerializeField] private Vector3 previewCubeLocalScale = new Vector3(110f, 220f, 110f);

    private Font uiFont;
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<string> tabCategories = new List<string>();
    private string activeCategory;
    private GameObject previewCube;
    private MeshRenderer previewRenderer;
    private Material[] previewOriginalMaterials;
    private Mesh previewOriginalMesh;
    private Coroutine visibilityRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(scene.name) || !scene.name.StartsWith("MainMenu"))
        {
            return;
        }

        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate.name != "VideoWindow" || candidate.gameObject.scene != scene)
            {
                continue;
            }

            if (candidate.GetComponent<CubeThemeUI>() == null)
            {
                candidate.gameObject.AddComponent<CubeThemeUI>();
            }

            // Edit'te açık unutulursa ana menüde 3D tema küpleri görünmesin
            candidate.gameObject.SetActive(false);

            return;
        }
    }

    private void OnEnable()
    {
        AutoBindIfNeeded();
        ResolveFont();

        if (!Application.isPlaying)
        {
            return;
        }

        CubeThemeManager.ThemeChanged -= OnThemeChanged;
        CubeThemeManager.ThemeChanged += OnThemeChanged;

        EnsurePreviewCubeRuntime();
        ApplyPreviewCubeTransform();
        if (previewCube != null)
        {
            previewCube.SetActive(true);
        }

        BindExistingCategoryTabs();
        activeCategory = SelectedCategoryOr("Metal");
        ApplyCategoryVisibility();
        ApplyPreviewMaterial();
        RefreshStatus();
        RefreshTabVisuals();

        if (visibilityRoutine != null)
        {
            StopCoroutine(visibilityRoutine);
        }

        visibilityRoutine = StartCoroutine(ApplyVisibilityAfterPanelOpens());
    }

    private void OnDisable()
    {
        CubeThemeManager.ThemeChanged -= OnThemeChanged;
        if (visibilityRoutine != null)
        {
            StopCoroutine(visibilityRoutine);
            visibilityRoutine = null;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || previewCube == null || !previewCube.activeInHierarchy)
        {
            return;
        }

        previewCube.transform.Rotate(0f, 35f * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    private void OnThemeChanged()
    {
        ApplyPreviewMaterial();
        RefreshStatus();
    }

    private IEnumerator ApplyVisibilityAfterPanelOpens()
    {
        // Panel Closed → Open animasyonunun ilk karesinde grup açılırsa 3D küpler dışarıda kalır
        yield return null;
        Canvas.ForceUpdateCanvases();

        Animator anim = GetComponent<Animator>();
        float timeout = 1.2f;
        float elapsed = 0f;
        while (elapsed < timeout && anim != null && anim.isActiveAndEnabled)
        {
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            if (!anim.IsInTransition(0) && info.IsName("Open") && info.normalizedTime >= 0.99f)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyCategoryVisibility();
        RefreshTabVisuals();
        visibilityRoutine = null;
    }

    private void AutoBindIfNeeded()
    {
        if (settingsRoot == null)
        {
            Transform found = transform.Find("Panel/Settings");
            if (found != null)
            {
                settingsRoot = found as RectTransform;
            }
        }

        if (settingsRoot == null)
        {
            return;
        }

        if (statusLabel == null)
        {
            Transform t = settingsRoot.Find("ThemeStatus");
            if (t != null)
            {
                statusLabel = t.GetComponent<Text>();
            }
        }

        if (previewAnchor == null)
        {
            Transform t = settingsRoot.Find("ThemePreviewAnchor");
            if (t != null)
            {
                previewAnchor = t as RectTransform;
            }
        }

        if (tabsRoot == null)
        {
            tabsRoot = settingsRoot.Find("ThemeTabs");
        }

        if (themesRoot == null)
        {
            Transform t = settingsRoot.Find("ThemeButtons");
            if (t == null)
            {
                t = settingsRoot.Find("ThemeScroll/Viewport/Content");
            }

            if (t == null)
            {
                t = settingsRoot.Find("ThemeScroll");
            }

            themesRoot = t;
        }
    }

    private void ResolveFont()
    {
        if (uiFont != null)
        {
            return;
        }

        if (statusLabel != null && statusLabel.font != null)
        {
            uiFont = statusLabel.font;
            return;
        }

        Text sample = GetComponentInChildren<Text>(true);
        uiFont = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void EnsurePreviewCubeRuntime()
    {
        if (previewCube != null || previewAnchor == null)
        {
            return;
        }

        Transform existing = previewAnchor.Find("ThemePreviewCube");
        if (existing != null)
        {
            previewCube = existing.gameObject;
            previewRenderer = previewCube.GetComponent<MeshRenderer>();
            ApplyPreviewCubeTransform();
            CachePreviewOriginals();
            return;
        }

        previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewCube.name = "ThemePreviewCube";
        previewCube.transform.SetParent(previewAnchor, false);

        Collider col = previewCube.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        previewRenderer = previewCube.GetComponent<MeshRenderer>();
        ApplyPreviewCubeTransform();
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            previewCube.layer = uiLayer;
        }

        CubeThemeCatalog catalog = Resources.Load<CubeThemeCatalog>("CubeThemeCatalog");
        if (catalog != null && previewRenderer != null)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                CubeThemeEntry entry = catalog.Get(i);
                if (entry != null && entry.material != null)
                {
                    previewRenderer.sharedMaterial = entry.material;
                    break;
                }
            }
        }

        CachePreviewOriginals();
    }

    private void ApplyPreviewCubeTransform()
    {
        if (previewCube == null)
        {
            return;
        }

        previewCube.transform.localPosition = previewCubeLocalPosition;
        previewCube.transform.localRotation = Quaternion.identity;
        previewCube.transform.localScale = previewCubeLocalScale;
    }

    private void CachePreviewOriginals()
    {
        if (previewRenderer == null)
        {
            return;
        }

        MeshFilter filter = previewRenderer.GetComponent<MeshFilter>();
        previewOriginalMesh = filter != null ? filter.sharedMesh : null;

        Material[] shared = previewRenderer.sharedMaterials;
        previewOriginalMaterials = new Material[shared.Length];
        for (int i = 0; i < shared.Length; i++)
        {
            previewOriginalMaterials[i] = shared[i];
        }
    }

    /// <summary>
    /// Edit Mode'da senin koyduğun sekmeleri kullanır. Hiçbirini silmez / yeniden üretmez.
    /// Kategori adı: Label metni, yoksa isimden (Tab_Metal → Metal).
    /// </summary>
    private void BindExistingCategoryTabs()
    {
        tabButtons.Clear();
        tabCategories.Clear();

        if (tabsRoot == null)
        {
            return;
        }

        for (int i = 0; i < tabsRoot.childCount; i++)
        {
            Transform child = tabsRoot.GetChild(i);
            Button button = child.GetComponent<Button>();
            if (button == null)
            {
                button = child.GetComponentInChildren<Button>(true);
            }

            // SF / Image tab'larda Button olmayabilir — tıklama için ekle
            if (button == null && child.GetComponent<UnityEngine.UI.Graphic>() != null)
            {
                button = child.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
            }

            if (button == null)
            {
                continue;
            }

            string category = ResolveTabCategory(child);
            string captured = category;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnTabClicked(captured));

            tabButtons.Add(button);
            tabCategories.Add(category);
        }

    }

    private static string ResolveTabCategory(Transform tab)
    {
        Text label = tab.GetComponentInChildren<Text>(true);
        if (label != null && !string.IsNullOrWhiteSpace(label.text))
        {
            return NormalizeCategory(label.text);
        }

        string name = tab.name;
        if (name.StartsWith("Tab_", System.StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(4);
        }

        return NormalizeCategory(name);
    }

    private static string NormalizeCategory(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
    }

    private void OnTabClicked(string category)
    {
        activeCategory = NormalizeCategory(category);
        ApplyCategoryVisibility();
        RefreshTabVisuals();
    }

    private void RefreshTabVisuals()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            Button button = tabButtons[i];
            if (button == null)
            {
                continue;
            }

            bool active = CategoryMatches(tabCategories[i], activeCategory);
            Image bg = button.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = active
                    ? new Color32(81, 238, 255, 255)
                    : new Color32(130, 160, 170, 255);
            }
        }
    }

    /// <summary>
    /// Grupları SetActive ile kapatmıyoruz — inactive objeler Panel animasyonunu kaçırır
    /// ve yeniden açılınca yanlış eksende kalır. CanvasGroup + renderer ile gizleriz.
    /// </summary>
    private void ApplyCategoryVisibility()
    {
        if (settingsRoot == null)
        {
            AutoBindIfNeeded();
        }

        if (settingsRoot == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(activeCategory))
        {
            activeCategory = "Metal";
        }

        bool anyShown = false;
        Transform metalGroup = null;

        for (int i = 0; i < settingsRoot.childCount; i++)
        {
            Transform child = settingsRoot.GetChild(i);
            if (!child.name.StartsWith("ThemeButtons", System.StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = child.name.Substring("ThemeButtons".Length);
            if (string.IsNullOrEmpty(suffix))
            {
                continue;
            }

            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
            }

            child.localRotation = Quaternion.identity;

            if (CategoryMatches(suffix, "Metal"))
            {
                metalGroup = child;
            }

            bool show = CategoryMatches(suffix, activeCategory);
            SetThemeGroupVisible(child, show);
            if (show)
            {
                anyShown = true;
            }
        }

        if (!anyShown && metalGroup != null)
        {
            activeCategory = "Metal";
            SetThemeGroupVisible(metalGroup, true);
        }

        Canvas.ForceUpdateCanvases();
    }

    private static void SetThemeGroupVisible(Transform root, bool visible)
    {
        if (root == null)
        {
            return;
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = root.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    private static bool CategoryMatches(string a, string b)
    {
        if (string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return CategoriesLooselyMatch(a, b);
    }

    private static bool CategoriesLooselyMatch(string a, string b)
    {
        if (string.Equals(a, "Wall", System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(b, "Stone", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(a, "Stone", System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(b, "Wall", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void ApplyPreviewMaterial()
    {
        if (previewRenderer == null || CubeThemeManager.Instance == null)
        {
            return;
        }

        CubeThemeEntry entry = CubeThemeManager.Instance.GetSelectedEntry();
        MeshFilter filter = previewRenderer.GetComponent<MeshFilter>();
        if (filter != null)
        {
            filter.sharedMesh = entry.mesh != null ? entry.mesh : previewOriginalMesh;
        }

        Material selected = entry.material;
        if (selected == null)
        {
            if (previewOriginalMaterials != null && previewOriginalMaterials.Length > 0)
            {
                previewRenderer.sharedMaterials = previewOriginalMaterials;
            }
        }
        else
        {
            Material[] mats = previewRenderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = selected;
            }

            previewRenderer.sharedMaterials = mats;
        }
    }

    private static string SelectedCategoryOr(string fallback)
    {
        CubeThemeEntry entry = CubeThemeManager.Instance != null ? CubeThemeManager.Instance.GetSelectedEntry() : null;
        return entry != null && !string.IsNullOrWhiteSpace(entry.category) ? entry.category.Trim() : fallback;
    }

    private void RefreshStatus()
    {
        if (statusLabel == null || CubeThemeManager.Instance == null)
        {
            return;
        }

        CubeThemeEntry entry = CubeThemeManager.Instance.GetSelectedEntry();
        string cat = NormalizeCategory(entry != null ? entry.category : null);
        string name = entry != null ? entry.displayName : "Default";
        statusLabel.text = cat + "  ·  " + name;
    }
}
