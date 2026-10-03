using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tutorial sistemi:
/// - Yeni mekanik kartı: bölümde ilk kez görülen her mekanik için bir kez (kamera karoya odaklanır).
/// - Kaydırma rehberi: World 1'in ilk bölümlerinde, bölüm bitirilene kadar çözüm yönünü gösterir.
/// - Duruma göre ipuçları: hedefe yatarak gelme, kırılgan karoda düşme, sert switch'e yatarak basma, art arda düşme.
/// </summary>
public sealed class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>Kart açıkken oyuncu girdisi engellenir.</summary>
    public static bool BlocksGameplay => Instance != null && Instance.cardOpen;

    private const string CardPrefPrefix = "Tut_Card_";
    private const string TipPrefPrefix = "Tut_Tip_";
    private const int FallsBeforeHintTip = 3;
    private const float CardTapDelay = 0.35f;
    private const float ToastDuration = 2.6f;
    private const float GuideSwipeLength = 170f;

    // World 1 bölüm -> rehberlik edilecek hamle sayısı (0 = çözümün tamamı)
    private static readonly Dictionary<int, int> GuidedLevelsWorld1 = new Dictionary<int, int>
    {
        { 1, 0 },
        { 2, 3 },
        { 3, 2 }
    };

    private static readonly TutorialMechanic[] AllMechanics =
        (TutorialMechanic[])Enum.GetValues(typeof(TutorialMechanic));

    private static readonly TutorialTip[] AllTips =
        (TutorialTip[])Enum.GetValues(typeof(TutorialTip));

    private RectTransform canvasRect;
    private Font font;
    private readonly List<Text> ownTexts = new List<Text>();

    private Sprite roundedSprite;
    private Sprite circleSprite;
    private Sprite ringSprite;
    private Sprite triangleSprite;

    private GameObject cardRoot;
    private CanvasGroup cardGroup;
    private RectTransform cardPanel;
    private Image cardAccentIcon;
    private Text cardTitle;
    private Text cardBody;
    private Text cardCounter;
    private Text cardFooter;
    private RectTransform markerRoot;
    private Image markerRing;
    private RectTransform markerArrow;

    private RectTransform toastRoot;
    private CanvasGroup toastGroup;
    private Text toastText;
    private Coroutine toastRoutine;
    private TutorialTip? visibleTip;

    private RectTransform guideRoot;
    private RectTransform guideTrail;
    private RectTransform guideDot;
    private RectTransform guideArrow;
    private Image guideDotImage;
    private Text guideLabel;

    private readonly Queue<TutorialMechanic> pendingCards = new Queue<TutorialMechanic>();
    private readonly Dictionary<TutorialMechanic, Vector2Int> cardCells = new Dictionary<TutorialMechanic, Vector2Int>();
    private Coroutine introRoutine;
    private bool cardOpen;
    private bool cardTapped;
    private float cardShownAt;
    private Transform markerTarget;
    private CameraFollow focusedCamera;
    private Transform cameraPreviousTarget;
    private Transform cameraFocusTarget;

    private bool guideActive;
    private string[] guideMoves;
    private int guideLimit;
    private int guideIndex;
    private bool guideShowLabel;
    private TumbleController guidePlayer;

    private int trackedWorld = -1;
    private int trackedLevel = -1;
    private int fallCount;
    private bool stuckTipShown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("TutorialManager");
        DontDestroyOnLoad(go);
        go.AddComponent<TutorialManager>();
    }

    /// <summary>Tüm kartları ve ipucu sayaçlarını sıfırlar (ayarlardan "Tutorial'ı tekrar göster" için).</summary>
    public static void ResetProgress()
    {
        foreach (TutorialMechanic mechanic in AllMechanics)
            PlayerPrefs.DeleteKey(CardPrefPrefix + mechanic);
        foreach (TutorialTip tip in AllTips)
            PlayerPrefs.DeleteKey(TipPrefPrefix + tip);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateSprites();
        BuildCanvas();
        HideAll();
    }

    private void OnEnable()
    {
        LevelLoader.OnLevelLoaded += OnLevelLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameplayEvents.PlayerMoved += OnPlayerMoved;
        GameplayEvents.PlayerFell += OnPlayerFell;
        GameplayEvents.GoalReachedLying += OnGoalReachedLying;
        GameplayEvents.HardSwitchPressedLying += OnHardSwitchPressedLying;
    }

    private void OnDisable()
    {
        LevelLoader.OnLevelLoaded -= OnLevelLoaded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameplayEvents.PlayerMoved -= OnPlayerMoved;
        GameplayEvents.PlayerFell -= OnPlayerFell;
        GameplayEvents.GoalReachedLying -= OnGoalReachedLying;
        GameplayEvents.HardSwitchPressedLying -= OnHardSwitchPressedLying;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── OLAYLAR ─────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideAll();
        ApplyFont(ResolveSceneFont());
    }

    private void OnLevelLoaded()
    {
        LevelLoader loader = LevelLoader.Instance;
        if (loader == null) return;

        if (loader.worldIndex != trackedWorld || loader.levelIndex != trackedLevel)
        {
            trackedWorld = loader.worldIndex;
            trackedLevel = loader.levelIndex;
            fallCount = 0;
            stuckTipShown = false;
        }

        StopGuide();
        if (introRoutine != null) StopCoroutine(introRoutine);
        CloseCardImmediate();

        pendingCards.Clear();
        cardCells.Clear();
        foreach (var entry in TutorialContent.DetectMechanics(loader.CurrentLevelData))
        {
            if (PlayerPrefs.GetInt(CardPrefPrefix + entry.Key, 0) == 1) continue;
            pendingCards.Enqueue(entry.Key);
            cardCells[entry.Key] = entry.Value;
        }

        introRoutine = StartCoroutine(LevelIntroRoutine());
    }

    private void OnPlayerMoved(Vector3 direction)
    {
        if (!guideActive || guideMoves == null || guideIndex >= guideMoves.Length) return;

        if (direction == ParseMove(guideMoves[guideIndex]))
            guideIndex++;
        else
            StopGuide();
    }

    private void OnPlayerFell(FallReason reason)
    {
        fallCount++;

        if (reason == FallReason.FragileTile && ShowTip(TutorialTip.FragileBroke, 3))
            return;

        if (fallCount >= FallsBeforeHintTip && !stuckTipShown && GameplaySettings.ShowHintButton)
        {
            stuckTipShown = true;
            ShowTip(TutorialTip.StuckUseHint, 5);
        }
    }

    private void OnGoalReachedLying() => ShowTip(TutorialTip.GoalLying, 4);

    private void OnHardSwitchPressedLying() => ShowTip(TutorialTip.HardSwitchLying, 4);

    // ─── YENİ MEKANİK KARTLARI ───────────────────────

    private IEnumerator LevelIntroRoutine()
    {
        // Oyuncu küpü ve kamera ilk karesini kursun
        yield return null;
        yield return null;

        while (TumbleController.isSimulating)
            yield return null;

        if (pendingCards.Count > 0)
        {
            int total = pendingCards.Count;
            int index = 0;
            cardOpen = true;

            while (pendingCards.Count > 0)
            {
                TutorialMechanic mechanic = pendingCards.Dequeue();
                index++;
                yield return ShowCardRoutine(mechanic, index, total);
                PlayerPrefs.SetInt(CardPrefPrefix + mechanic, 1);
                PlayerPrefs.Save();
            }

            cardOpen = false;
        }

        introRoutine = null;
        StartGuideIfNeeded();
    }

    private IEnumerator ShowCardRoutine(TutorialMechanic mechanic, int index, int total)
    {
        TutorialContent.Card card = TutorialContent.GetCard(mechanic);
        cardTitle.text = card.Title;
        cardBody.text = card.Body;
        cardAccentIcon.color = card.Accent;
        markerRing.color = card.Accent;
        markerArrow.GetComponent<Image>().color = card.Accent;
        cardCounter.text = total > 1 ? $"{index}/{total}" : string.Empty;
        cardFooter.text = TutorialContent.TapToContinue;

        GameObject tile = null;
        if (cardCells.TryGetValue(mechanic, out Vector2Int cell) && LevelLoader.Instance != null)
            tile = LevelLoader.Instance.GetTileAt(cell.x, cell.y);

        if (tile != null)
        {
            FocusCamera(tile.transform);
            markerTarget = tile.transform;
        }

        cardGroup.alpha = 0f;
        cardRoot.SetActive(true);
        cardTapped = false;
        cardShownAt = Time.unscaledTime;

        yield return Fade(cardGroup, cardPanel, 0f, 1f, 0.25f);

        while (!cardTapped)
            yield return null;

        yield return Fade(cardGroup, cardPanel, 1f, 0f, 0.15f);

        cardRoot.SetActive(false);
        markerTarget = null;
        RestoreCamera();
    }

    private void OnCardTapped()
    {
        if (Time.unscaledTime - cardShownAt < CardTapDelay) return;
        cardTapped = true;
    }

    private void CloseCardImmediate()
    {
        cardOpen = false;
        cardTapped = false;
        markerTarget = null;
        if (cardRoot != null) cardRoot.SetActive(false);
        RestoreCamera();
    }

    private void FocusCamera(Transform target)
    {
        focusedCamera = FindAnyObjectByType<CameraFollow>();
        if (focusedCamera == null) return;

        cameraPreviousTarget = focusedCamera.target;
        cameraFocusTarget = target;
        focusedCamera.target = target;
    }

    private void RestoreCamera()
    {
        if (focusedCamera != null && focusedCamera.target == cameraFocusTarget)
        {
            Transform restore = cameraPreviousTarget;
            if (restore == null)
            {
                TumbleController player = FindPlayer();
                restore = player != null ? player.transform : null;
            }

            focusedCamera.target = restore;
        }

        focusedCamera = null;
        cameraFocusTarget = null;
        cameraPreviousTarget = null;
    }

    private static IEnumerator Fade(CanvasGroup group, RectTransform panel, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            float v = Mathf.Lerp(from, to, k);
            group.alpha = v;
            panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, v);
            yield return null;
        }

        group.alpha = to;
        panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, to);
    }

    // ─── KAYDIRMA REHBERİ ────────────────────────────

    private void StartGuideIfNeeded()
    {
        LevelLoader loader = LevelLoader.Instance;
        if (loader == null || loader.worldIndex != 1) return;
        if (!GuidedLevelsWorld1.TryGetValue(loader.levelIndex, out int limit)) return;
        if (LevelStarRating.HasPlayedLevel(loader.worldIndex, loader.levelIndex)) return;

        LevelData data = loader.CurrentLevelData;
        if (data == null || string.IsNullOrWhiteSpace(data.hintMoves)) return;

        guideMoves = data.hintMoves.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (guideMoves.Length == 0) return;

        guideLimit = limit <= 0 ? guideMoves.Length : Mathf.Min(limit, guideMoves.Length);
        guideIndex = 0;
        guideShowLabel = loader.levelIndex == 1;
        guidePlayer = FindPlayer();
        guideActive = true;
    }

    private void StopGuide()
    {
        guideActive = false;
        guideMoves = null;
        guidePlayer = null;
        if (guideRoot != null) guideRoot.gameObject.SetActive(false);
    }

    private void UpdateGuide()
    {
        if (!guideActive)
            return;

        if (guidePlayer == null)
            guidePlayer = FindPlayer();

        if (guidePlayer == null)
        {
            guideRoot.gameObject.SetActive(false);
            return;
        }

        // Hamle sayısı rehberle uyuşmuyorsa (ipucu oynatıldı vb.) rehberi bırak
        if (guideIndex >= guideLimit || guidePlayer.CurrentMoveCount != guideIndex)
        {
            StopGuide();
            return;
        }

        bool blocked = GameUIController.Instance != null && GameUIController.Instance.BlocksGameplay;
        bool canShow = !cardOpen && !blocked && !guidePlayer.IsMoving && !guidePlayer.isSplit
                       && !TumbleController.isSimulating;

        Camera cam = Camera.main;
        if (!canShow || cam == null)
        {
            guideRoot.gameObject.SetActive(false);
            return;
        }

        Vector3 worldDir = ParseMove(guideMoves[guideIndex]);
        Vector3 origin = guidePlayer.transform.position;
        Vector3 screenA = cam.WorldToScreenPoint(origin);
        Vector3 screenB = cam.WorldToScreenPoint(origin + worldDir);
        if (screenA.z < 0f || screenB.z < 0f)
        {
            guideRoot.gameObject.SetActive(false);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenA, null, out Vector2 start) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenB, null, out Vector2 next))
        {
            guideRoot.gameObject.SetActive(false);
            return;
        }

        Vector2 dir = (next - start).normalized;
        if (dir.sqrMagnitude < 0.01f)
        {
            guideRoot.gameObject.SetActive(false);
            return;
        }

        guideRoot.gameObject.SetActive(true);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        guideTrail.anchoredPosition = start;
        guideTrail.localEulerAngles = new Vector3(0f, 0f, angle);
        guideTrail.sizeDelta = new Vector2(GuideSwipeLength, 12f);

        guideArrow.anchoredPosition = start + dir * (GuideSwipeLength + 18f);
        guideArrow.localEulerAngles = new Vector3(0f, 0f, angle);

        const float cycle = 1.2f;
        const float travel = 0.8f;
        float phase = (Time.unscaledTime % cycle) / travel;
        float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase));
        float alpha = phase < 0.15f ? phase / 0.15f : (phase > 1f ? 0f : 1f - Mathf.Clamp01((phase - 0.75f) / 0.25f));

        guideDot.anchoredPosition = start + dir * (GuideSwipeLength * move);
        guideDotImage.color = new Color(1f, 1f, 1f, 0.9f * alpha);

        guideLabel.gameObject.SetActive(guideShowLabel && guideIndex == 0);
        guideLabel.rectTransform.anchoredPosition = start + new Vector2(0f, 120f);
    }

    // ─── KISA İPUÇLARI ───────────────────────────────

    /// <returns>İpucu gösterildiyse true.</returns>
    private bool ShowTip(TutorialTip tip, int maxTimes)
    {
        if (cardOpen) return false;
        if (visibleTip == tip) return false;

        string key = TipPrefPrefix + tip;
        int shown = PlayerPrefs.GetInt(key, 0);
        if (shown >= maxTimes) return false;

        PlayerPrefs.SetInt(key, shown + 1);
        PlayerPrefs.Save();

        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(tip));
        return true;
    }

    private IEnumerator ToastRoutine(TutorialTip tip)
    {
        visibleTip = tip;
        toastText.text = TutorialContent.GetTip(tip);
        toastRoot.gameObject.SetActive(true);

        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Clamp01(t / 0.2f);
            yield return null;
        }

        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(ToastDuration);

        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - Mathf.Clamp01(t / 0.3f);
            yield return null;
        }

        toastRoot.gameObject.SetActive(false);
        visibleTip = null;
        toastRoutine = null;
    }

    // ─── KARE GÜNCELLEMELERİ ─────────────────────────

    private void Update()
    {
        UpdateGuide();
    }

    private void LateUpdate()
    {
        if (cardRoot != null && cardRoot.activeSelf)
        {
            float width = Mathf.Min(900f, canvasRect.rect.width - 60f);
            cardPanel.sizeDelta = new Vector2(width, cardPanel.sizeDelta.y);
            cardFooter.color = new Color(1f, 1f, 1f, 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f));
            UpdateMarker();
        }

        if (toastRoot != null && toastRoot.gameObject.activeSelf)
        {
            float width = Mathf.Min(820f, canvasRect.rect.width - 60f);
            toastRoot.sizeDelta = new Vector2(width, toastRoot.sizeDelta.y);
        }
    }

    private void UpdateMarker()
    {
        Camera cam = Camera.main;
        if (markerTarget == null || cam == null)
        {
            markerRoot.gameObject.SetActive(false);
            return;
        }

        Vector3 world = markerTarget.position;
        world.y = 0f;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            markerRoot.gameObject.SetActive(false);
            return;
        }

        markerRoot.gameObject.SetActive(true);
        markerRoot.anchoredPosition = local;

        float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
        markerRing.rectTransform.localScale = Vector3.one * pulse;
        markerArrow.anchoredPosition = new Vector2(0f, 110f + 14f * Mathf.Sin(Time.unscaledTime * 4f));
    }

    // ─── YARDIMCILAR ─────────────────────────────────

    private void HideAll()
    {
        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = null;
        pendingCards.Clear();
        CloseCardImmediate();
        StopGuide();

        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = null;
        visibleTip = null;
        if (toastRoot != null) toastRoot.gameObject.SetActive(false);
    }

    private static TumbleController FindPlayer()
    {
        foreach (TumbleController tc in FindObjectsByType<TumbleController>())
        {
            if (!tc.decorativeMode) return tc;
        }

        return null;
    }

    private static Vector3 ParseMove(string move)
    {
        switch (move.ToUpperInvariant())
        {
            case "F": return Vector3.forward;
            case "B": return Vector3.back;
            case "L": return Vector3.left;
            case "R": return Vector3.right;
            default: return Vector3.zero;
        }
    }

    private static Font ResolveSceneFont() => GameFont.Resolve();

    private void ApplyFont(Font newFont)
    {
        if (newFont == null) return;
        font = newFont;
        foreach (Text text in ownTexts)
        {
            if (text != null) text.font = font;
        }
    }

    // ─── UI KURULUMU ─────────────────────────────────

    private void BuildCanvas()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("TutorialCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasRect = (RectTransform)canvasGo.transform;

        BuildGuide(canvasRect);
        BuildToast(canvasRect);
        BuildCard(canvasRect);
    }

    private void BuildCard(RectTransform parent)
    {
        cardRoot = new GameObject("TutorialCard", typeof(RectTransform));
        RectTransform rootRect = (RectTransform)cardRoot.transform;
        rootRect.SetParent(parent, false);
        Stretch(rootRect);
        cardGroup = cardRoot.AddComponent<CanvasGroup>();

        Image dim = CreateImage("Dim", rootRect, null, new Color(0f, 0f, 0f, 0.55f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        Button tap = dim.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(OnCardTapped);

        markerRoot = new GameObject("Marker", typeof(RectTransform)).GetComponent<RectTransform>();
        markerRoot.SetParent(rootRect, false);
        Center(markerRoot, Vector2.zero);
        markerRing = CreateImage("Ring", markerRoot, ringSprite, Color.white);
        markerRing.rectTransform.sizeDelta = new Vector2(150f, 150f);
        Image arrow = CreateImage("Arrow", markerRoot, triangleSprite, Color.white);
        markerArrow = arrow.rectTransform;
        markerArrow.sizeDelta = new Vector2(64f, 64f);
        markerArrow.localEulerAngles = new Vector3(0f, 0f, -90f);

        Image panel = CreateImage("Panel", rootRect, roundedSprite, new Color(0.09f, 0.10f, 0.14f, 0.97f));
        panel.type = Image.Type.Sliced;
        cardPanel = panel.rectTransform;
        cardPanel.anchorMin = cardPanel.anchorMax = new Vector2(0.5f, 0f);
        cardPanel.pivot = new Vector2(0.5f, 0f);
        cardPanel.anchoredPosition = new Vector2(0f, 70f);
        cardPanel.sizeDelta = new Vector2(900f, 330f);

        cardAccentIcon = CreateImage("Accent", cardPanel, circleSprite, Color.white);
        RectTransform iconRect = cardAccentIcon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(40f, -40f);
        iconRect.sizeDelta = new Vector2(44f, 44f);

        cardTitle = CreateText("Title", cardPanel, 44, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetTopStretch(cardTitle.rectTransform, 100f, 120f, -30f, 64f);

        cardCounter = CreateText("Counter", cardPanel, 28, FontStyle.Normal, TextAnchor.MiddleRight,
            new Color(1f, 1f, 1f, 0.5f));
        RectTransform counterRect = cardCounter.rectTransform;
        counterRect.anchorMin = counterRect.anchorMax = new Vector2(1f, 1f);
        counterRect.pivot = new Vector2(1f, 1f);
        counterRect.anchoredPosition = new Vector2(-40f, -30f);
        counterRect.sizeDelta = new Vector2(120f, 64f);

        cardBody = CreateText("Body", cardPanel, 32, FontStyle.Normal, TextAnchor.UpperLeft,
            new Color(0.86f, 0.89f, 0.94f));
        cardBody.resizeTextForBestFit = true;
        cardBody.resizeTextMinSize = 22;
        cardBody.resizeTextMaxSize = 32;
        cardBody.lineSpacing = 1.1f;
        RectTransform bodyRect = cardBody.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(40f, 80f);
        bodyRect.offsetMax = new Vector2(-40f, -110f);

        cardFooter = CreateText("Footer", cardPanel, 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        RectTransform footerRect = cardFooter.rectTransform;
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 24f);
        footerRect.sizeDelta = new Vector2(0f, 44f);

        cardRoot.SetActive(false);
    }

    private void BuildToast(RectTransform parent)
    {
        Image bg = CreateImage("Toast", parent, roundedSprite, new Color(0.09f, 0.10f, 0.14f, 0.92f));
        bg.type = Image.Type.Sliced;
        toastRoot = bg.rectTransform;
        toastRoot.anchorMin = toastRoot.anchorMax = new Vector2(0.5f, 1f);
        toastRoot.pivot = new Vector2(0.5f, 1f);
        toastRoot.anchoredPosition = new Vector2(0f, -190f);
        toastRoot.sizeDelta = new Vector2(820f, 90f);
        toastGroup = bg.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;

        toastText = CreateText("Text", toastRoot, 32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        toastText.resizeTextForBestFit = true;
        toastText.resizeTextMinSize = 20;
        toastText.resizeTextMaxSize = 32;
        RectTransform textRect = toastText.rectTransform;
        Stretch(textRect);
        textRect.offsetMin = new Vector2(30f, 10f);
        textRect.offsetMax = new Vector2(-30f, -10f);

        toastRoot.gameObject.SetActive(false);
    }

    private void BuildGuide(RectTransform parent)
    {
        guideRoot = new GameObject("SwipeGuide", typeof(RectTransform)).GetComponent<RectTransform>();
        guideRoot.SetParent(parent, false);
        Stretch(guideRoot);

        Image trail = CreateImage("Trail", guideRoot, roundedSprite, new Color(1f, 1f, 1f, 0.35f));
        trail.type = Image.Type.Sliced;
        guideTrail = trail.rectTransform;
        guideTrail.anchorMin = guideTrail.anchorMax = new Vector2(0.5f, 0.5f);
        guideTrail.pivot = new Vector2(0f, 0.5f);

        Image arrow = CreateImage("Arrow", guideRoot, triangleSprite, new Color(1f, 1f, 1f, 0.85f));
        guideArrow = arrow.rectTransform;
        Center(guideArrow, new Vector2(56f, 56f));

        guideDotImage = CreateImage("Dot", guideRoot, circleSprite, Color.white);
        guideDot = guideDotImage.rectTransform;
        Center(guideDot, new Vector2(70f, 70f));

        guideLabel = CreateText("Label", guideRoot, 40, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        guideLabel.text = TutorialContent.GetTip(TutorialTip.Swipe);
        Outline outline = guideLabel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);
        Center(guideLabel.rectTransform, new Vector2(400f, 70f));

        guideRoot.gameObject.SetActive(false);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string name, Transform parent, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        ownTexts.Add(text);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
    }

    private static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, top - height);
        rect.offsetMax = new Vector2(-right, top);
    }

    // ─── PROSEDÜREL SPRITE'LAR ───────────────────────

    private void CreateSprites()
    {
        roundedSprite = CreateRoundedSprite(96, 32);
        circleSprite = CreateRoundedSprite(128, 64);
        ringSprite = CreateRingSprite(256, 14f);
        triangleSprite = CreateTriangleSprite(128);
    }

    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D tex = NewTexture(size);
        var pixels = new Color32[size * size];
        float inner = radius - 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(inner - d + 1f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static Sprite CreateRingSprite(int size, float thickness)
    {
        Texture2D tex = NewTexture(size);
        var pixels = new Color32[size * size];
        float center = size * 0.5f;
        float outer = center - 1f;
        float inner = outer - thickness;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Sağa bakan üçgen.</summary>
    private static Sprite CreateTriangleSprite(int size)
    {
        Texture2D tex = NewTexture(size);
        var pixels = new Color32[size * size];
        Vector2 a = new Vector2(0.18f, 0.12f) * size;
        Vector2 b = new Vector2(0.18f, 0.88f) * size;
        Vector2 c = new Vector2(0.90f, 0.50f) * size;
        const int samples = 4;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        Vector2 p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                        if (InTriangle(p, a, b, c)) inside++;
                    }
                }

                byte alpha = (byte)(255f * inside / (samples * samples));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }

    private static Texture2D NewTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        return tex;
    }
}
