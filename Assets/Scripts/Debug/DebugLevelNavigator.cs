using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Test için oyun ekranının üstüne bölüm/dünya ileri-geri barı koyar.
/// Yalnızca LevelLoader'daki "Show Level Navigator" işaretliyken görünür ve çalışır.
/// Kısayollar: PageUp = önceki bölüm, PageDown = sonraki bölüm.
/// </summary>
public sealed class DebugLevelNavigator : MonoBehaviour
{
    private static readonly SortedDictionary<int, int> LevelCounts = new SortedDictionary<int, int>();

    private GameObject canvasRoot;
    private Text label;
    private RectTransform bar;
    private Rect lastSafe;
    private Vector2Int lastScreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate();
    }

    private static void TryCreate()
    {
        if (LevelLoader.Instance == null || FindAnyObjectByType<DebugLevelNavigator>() != null)
        {
            return;
        }

        new GameObject("DebugLevelNavigator").AddComponent<DebugLevelNavigator>();
    }

    private void Awake()
    {
        BuildLevelCounts();
        BuildUI();
        canvasRoot.SetActive(LevelLoader.Instance != null && LevelLoader.Instance.showLevelNavigator);
    }

    private void Update()
    {
        bool show = LevelLoader.Instance != null && LevelLoader.Instance.showLevelNavigator;
        if (canvasRoot.activeSelf != show)
        {
            canvasRoot.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.pageDownKey.wasPressedThisFrame) StepLevel(1);
            if (kb.pageUpKey.wasPressedThisFrame) StepLevel(-1);
        }

        label.text = $"W{LevelLoader.Instance.worldIndex} · L{LevelLoader.Instance.levelIndex}";
    }

    private void LateUpdate()
    {
        if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
        {
            PlaceInSafeArea();
        }
    }

    private static void BuildLevelCounts()
    {
        if (LevelCounts.Count > 0)
        {
            return;
        }

        foreach (TextAsset asset in Resources.LoadAll<TextAsset>("LevelsJSON"))
        {
            string n = asset.name;
            int l = n.IndexOf('l');
            if (n.Length < 4 || n[0] != 'w' || l < 2) continue;
            if (!int.TryParse(n.Substring(1, l - 1), out int world)) continue;
            if (!int.TryParse(n.Substring(l + 1), out int level)) continue;

            LevelCounts.TryGetValue(world, out int max);
            LevelCounts[world] = Mathf.Max(max, level);
        }
    }

    private void StepLevel(int dir)
    {
        LevelLoader loader = LevelLoader.Instance;
        if (loader == null || LevelCounts.Count == 0)
        {
            return;
        }

        int world = loader.worldIndex;
        int level = loader.levelIndex + dir;
        if (level < 1)
        {
            world = NeighbourWorld(world, -1);
            level = LevelCounts[world];
        }
        else if (!LevelCounts.TryGetValue(world, out int count) || level > count)
        {
            world = NeighbourWorld(world, 1);
            level = 1;
        }

        loader.LoadLevel(world, level);
    }

    private void StepWorld(int dir)
    {
        LevelLoader loader = LevelLoader.Instance;
        if (loader == null || LevelCounts.Count == 0)
        {
            return;
        }

        loader.LoadLevel(NeighbourWorld(loader.worldIndex, dir), 1);
    }

    private static int NeighbourWorld(int world, int dir)
    {
        var worlds = new List<int>(LevelCounts.Keys);
        int i = worlds.IndexOf(world);
        if (i < 0) return worlds[0];
        return worlds[(i + dir + worlds.Count) % worlds.Count];
    }

    private void BuildUI()
    {
        Font font = GameFont.Resolve();

        var canvasGo = new GameObject("DebugLevelCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasRoot = canvasGo;
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        barGo.transform.SetParent(canvasGo.transform, false);
        bar = (RectTransform)barGo.transform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(560f, 70f);
        barGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        var layout = barGo.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        AddButton(font, "<<", 70f, () => StepWorld(-1));
        AddButton(font, "<", 70f, () => StepLevel(-1));
        label = AddText(font, "", 220f, barGo.transform);
        AddButton(font, ">", 70f, () => StepLevel(1));
        AddButton(font, ">>", 70f, () => StepWorld(1));

        PlaceInSafeArea();
    }

    private void AddButton(Font font, string text, float width, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(bar, false);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
        go.GetComponent<LayoutElement>().preferredWidth = width;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        Text t = AddText(font, text, width, go.transform);
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Text AddText(Font font, string text, float width, Transform parent)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredWidth = width;
        Text t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = 30;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private void PlaceInSafeArea()
    {
        lastSafe = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (bar == null || Screen.height <= 0)
        {
            return;
        }

        float topInsetPx = Screen.height - lastSafe.yMax;
        float unitsPerPx = 1080f / Screen.height;
        bar.anchoredPosition = new Vector2(0f, -(topInsetPx * unitsPerPx + 12f));
    }
}
