using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hint (ipucu) butonu ve hamle sayacını yönetir.
/// </summary>
public class HintController : MonoBehaviour
{
    [Header("Hint Ayarları")]
    [Tooltip("Hamleler arası bekleme süresi (saniye)")]
    public float moveDelay = 0.4f;

    [Tooltip("İlk kurulumda verilecek ücretsiz hint hakkı")]
    public int defaultHintCredits = 3;

    [Header("Reklam")]
    [Tooltip("AD basınca reklam sonrası otomatik hint başlat")]
    [SerializeField] private bool autoPlayHintAfterAd = true;

    [Header("UI Referansları")]
    [Tooltip("Canvas altındaki sahne Hint butonu. Boşsa Canvas/HintButton aranır.")]
    public Button sceneHintButton;

    [Tooltip("Canvas altındaki hamle sayacı Text. Boşsa Canvas/MoveCounterLabel aranır.")]
    public Text sceneMoveCounterLabel;

    private const string PrefKeyHintCredits = "HintCredits";
    private const string AdLabel = "AD";

    private Button hintButton;
    private Text hintButtonText;
    private Text moveCounterText;
    private TumbleController trackedPlayer;
    private bool isPlaying = false;
    private bool hintSessionCharged = false;
    private bool waitingForAd = false;

    // Kaldığı yeri hafızada tut
    private int resumeMoveIndex = 0;
    private string[] savedMoves = null;
    private int savedWorldIndex = -1;
    private int savedLevelIndex = -1;

    private GameObject loadingOverlay; // Simülasyon sirasinda görünecek loading paneli

    private void Start()
    {
        EnsureAdServiceExists();
        CreateFullTestUI();
    }

    private static void EnsureAdServiceExists()
    {
        if (AdsManager.Instance == null)
        {
            var adsGo = new GameObject("AdsManager");
            DontDestroyOnLoad(adsGo);
            adsGo.AddComponent<AdsManager>();
        }

        if (HintRewardedAdService.Instance != null) return;
        var go = new GameObject("HintRewardedAdService");
        go.AddComponent<HintRewardedAdService>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        LevelLoader.OnLevelLoaded += ResetHint;
        GameplaySettings.SettingsChanged += ApplyHintButtonVisibility;
        ApplyHintButtonVisibility();
    }

    private void OnDisable()
    {
        LevelLoader.OnLevelLoaded -= ResetHint;
        GameplaySettings.SettingsChanged -= ApplyHintButtonVisibility;
    }

    /// <summary>Level yüklenince ipucunu sıfırla.</summary>
    private void ResetHint()
    {
        StopAllCoroutines();
        isPlaying = false;
        resumeMoveIndex = 0;
        savedMoves = null;
        savedWorldIndex = -1;
        savedLevelIndex = -1;
        trackedPlayer = null;
        hintSessionCharged = false;
        RefreshHintCreditsLabel();
        ApplyHintButtonVisibility();
        UpdateMoveCounter();
    }

    // ─── UI OLUŞTURMA ────────────────────────────────
    private void CreateFullTestUI()
    {
        // EventSystem check
        System.Type inputModuleType =
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");

        var existingES = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (existingES != null && inputModuleType != null)
        {
            var standalone = existingES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                standalone.enabled = false; // Hata vermesini önlemek için önce devre dışı bırakıyoruz
                DestroyImmediate(standalone);
                if (existingES.GetComponent(inputModuleType) == null)
                    existingES.gameObject.AddComponent(inputModuleType);
            }
        }
        if (existingES == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            if (inputModuleType != null) es.AddComponent(inputModuleType);
            else es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Canvas check
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("TestUICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Sahne Hint butonu (Canvas sol alt) — runtime'da ikinci buton üretme
        BindSceneHintButton(canvas.transform);

        // 2. Hamle sayacı (Canvas sağ üst, sahnede hazır)
        BindSceneMoveCounter(canvas.transform);

        // 5. Loading Overlay (Simülasyon karartma ekranı)
        CreateLoadingOverlay(canvas.transform, defaultFont);
    }

    private void BindSceneHintButton(Transform canvas)
    {
        Button btn = sceneHintButton;
        if (btn == null)
        {
            Transform hintTransform = canvas.Find("HintButton");
            if (hintTransform != null)
                btn = hintTransform.GetComponent<Button>();
        }

        if (btn == null)
        {
            Debug.LogWarning("[HintController] Canvas altında HintButton bulunamadı.");
            return;
        }

        hintButton = btn;

        // LabelHint'i tercih et; yoksa ilk Text child
        Transform labelHint = btn.transform.Find("Background/LabelHint")
            ?? btn.transform.Find("LabelHint");
        if (labelHint != null)
            hintButtonText = labelHint.GetComponent<Text>();
        if (hintButtonText == null)
            hintButtonText = btn.GetComponentInChildren<Text>(true);

        // Persistent dinleyicileri temizle; sadece hint aksiyonu kalsın
        btn.onClick = new Button.ButtonClickedEvent();
        btn.onClick.AddListener(OnHintPressed);

        EnsureHintCreditsInitialized();
        RefreshHintCreditsLabel();
        ApplyHintButtonVisibility();
    }

    private void ApplyHintButtonVisibility()
    {
        if (hintButton == null)
        {
            return;
        }

        hintButton.gameObject.SetActive(GameplaySettings.ShowHintButton);
    }

    private void Update()
    {
        UpdateMoveCounter();

        if (loadingOverlay != null)
        {
            // TumbleController.isSimulating durumuna göre loading panelini aktif/deaktif et
            loadingOverlay.SetActive(TumbleController.isSimulating);
        }
    }

    private void BindSceneMoveCounter(Transform canvas)
    {
        Text label = sceneMoveCounterLabel;
        if (label == null)
        {
            Transform labelTransform = canvas.Find("MoveCounterLabel");
            if (labelTransform == null)
                labelTransform = canvas.Find("MoveCounterPanel/MoveCounterLabel");
            if (labelTransform != null)
                label = labelTransform.GetComponent<Text>();
        }

        if (label == null)
        {
            Debug.LogWarning("[HintController] Canvas altında MoveCounterLabel bulunamadı.");
            return;
        }

        moveCounterText = label;
        UpdateMoveCounter();
    }

    private void UpdateMoveCounter()
    {
        if (moveCounterText == null) return;

        if (trackedPlayer == null)
            trackedPlayer = FindObjectOfType<TumbleController>();

        int playedMoves = trackedPlayer != null ? trackedPlayer.CurrentMoveCount : 0;
        int minMoves = GetMinimumMoveCount();
        moveCounterText.text = $"MOVE : {playedMoves}\nTARGET : {minMoves}";
    }

    private int GetMinimumMoveCount()
    {
        LevelData data = LevelLoader.Instance != null ? LevelLoader.Instance.CurrentLevelData : null;
        if (data == null) return 0;
        if (data.minMoves > 0) return data.minMoves;
        if (string.IsNullOrWhiteSpace(data.hintMoves)) return 0;

        return data.hintMoves.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private void CreateLoadingOverlay(Transform parent, Font font)
    {
        loadingOverlay = new GameObject("LoadingOverlay");
        loadingOverlay.transform.SetParent(parent, false);

        RectTransform rect = loadingOverlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        // Arka Plan: Yarı saydam çok şık premium koyu bir renk (glassmorphism/dark mode esintisi)
        Image img = loadingOverlay.AddComponent<Image>();
        img.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

        // Ana Başlık
        GameObject textObj = new GameObject("Title");
        textObj.transform.SetParent(loadingOverlay.transform, false);
        RectTransform titleRect = textObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 25f);
        titleRect.sizeDelta = new Vector2(0f, 60f);

        Text t = textObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 24;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.2f, 0.65f, 1f, 1f); // Neon Mavi/Siyan renk tonu
        t.text = "BÖLÜM ANALİZ EDİLİYOR...";

        // Alt Bilgi Yazısı
        GameObject subTextObj = new GameObject("Sub");
        subTextObj.transform.SetParent(loadingOverlay.transform, false);
        RectTransform subRect = subTextObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0.5f);
        subRect.anchorMax = new Vector2(1f, 0.5f);
        subRect.anchoredPosition = new Vector2(0f, -25f);
        subRect.sizeDelta = new Vector2(0f, 40f);

        Text subT = subTextObj.AddComponent<Text>();
        subT.font = font;
        subT.fontSize = 14;
        subT.fontStyle = FontStyle.Normal;
        subT.alignment = TextAnchor.MiddleCenter;
        subT.color = new Color(0.7f, 0.75f, 0.8f, 1f);
        subT.text = "Yol haritasi ve ipuçlari çikariliyor, lütfen bekleyiniz...";

        loadingOverlay.SetActive(false);
    }

    // ─── BUTON EYLEMLERİ ─────────────────────────────
    public void OnHintPressed()
    {
        if (waitingForAd) return;

        if (isPlaying)
        {
            isPlaying = false;
            return;
        }

        // Hakkı yoksa rewarded reklam
        if (GetHintCredits() <= 0)
        {
            RefreshHintCreditsLabel();
            OnHintAdRequested();
            return;
        }

        BeginHintSpendingCredit();
    }

    private void BeginHintSpendingCredit()
    {
        // Yeni oturumda 1 hak düş; duraklatıp devam ederken tekrar düşme
        if (!hintSessionCharged)
        {
            SpendHintCredit();
            hintSessionCharged = true;
        }

        StartCoroutine(PlayHint());
    }

    /// <summary>Reklam sonrası çağrılacak: hint hakkı ekler ve label'ı günceller.</summary>
    public void GrantHintsFromAd(int amount = 1)
    {
        if (amount <= 0) return;
        SetHintCredits(GetHintCredits() + amount);
        RefreshHintCreditsLabel();
    }

    private void OnHintAdRequested()
    {
        EnsureAdServiceExists();
        var ads = HintRewardedAdService.Instance;
        if (ads == null)
        {
            Debug.LogWarning("[HintController] HintRewardedAdService yok.");
            return;
        }

        waitingForAd = true;
        if (hintButton != null) hintButton.interactable = false;
        if (hintButtonText != null) hintButtonText.text = "...";

        ads.ShowRewarded(success =>
        {
            waitingForAd = false;
            if (hintButton != null) hintButton.interactable = true;

            if (!success)
            {
                RefreshHintCreditsLabel();
                Debug.Log("[HintController] Reklam tamamlanmadı / iptal.");
                return;
            }

            GrantHintsFromAd(ads.HintsGrantedPerAd);

            if (autoPlayHintAfterAd && GetHintCredits() > 0 && !isPlaying)
                BeginHintSpendingCredit();
            else
                RefreshHintCreditsLabel();
        });
    }

    private void EnsureHintCreditsInitialized()
    {
        if (!PlayerPrefs.HasKey(PrefKeyHintCredits))
        {
            PlayerPrefs.SetInt(PrefKeyHintCredits, Mathf.Max(0, defaultHintCredits));
            PlayerPrefs.Save();
        }
    }

    private int GetHintCredits()
    {
        EnsureHintCreditsInitialized();
        return Mathf.Max(0, PlayerPrefs.GetInt(PrefKeyHintCredits, defaultHintCredits));
    }

    private void SetHintCredits(int value)
    {
        PlayerPrefs.SetInt(PrefKeyHintCredits, Mathf.Max(0, value));
        PlayerPrefs.Save();
    }

    private void SpendHintCredit()
    {
        SetHintCredits(GetHintCredits() - 1);
        RefreshHintCreditsLabel();
    }

    private void RefreshHintCreditsLabel()
    {
        if (hintButtonText == null) return;

        int credits = GetHintCredits();
        hintButtonText.text = credits > 0 ? credits.ToString() : AdLabel;
    }

    private IEnumerator PlayHint()
    {
        isPlaying = true;

        LevelLoader loader = LevelLoader.Instance;
        if (loader == null)
        {
            Debug.LogError("[HintController] LevelLoader bulunamadı!");
            Finish(allDone: false);
            yield break;
        }

        if (loader.worldIndex != savedWorldIndex || loader.levelIndex != savedLevelIndex)
        {
            resumeMoveIndex = 0;
            savedMoves = null;
            savedWorldIndex = loader.worldIndex;
            savedLevelIndex = loader.levelIndex;
        }

        if (savedMoves == null)
        {
            if (loader.CurrentLevelData == null || string.IsNullOrWhiteSpace(loader.CurrentLevelData.hintMoves))
            {
                Debug.LogWarning($"[HintController] w{loader.worldIndex}l{loader.levelIndex} için ipucu yok.");
                Finish(allDone: false);
                yield break;
            }

            savedMoves = loader.CurrentLevelData.hintMoves.Split(
                new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        TumbleController tc = FindObjectOfType<TumbleController>();
        if (tc == null)
        {
            Debug.LogError("[HintController] TumbleController bulunamadı!");
            Finish(allDone: false);
            yield break;
        }

        // Eğer ipucu ilk defa (devam edilmeden) başlatılıyorsa en son çakıştığı doğru konuma al
        if (resumeMoveIndex == 0)
        {
            bool waitingForRewind = true;
            StartCoroutine(tc.MatchAndRewind((resultIndex) => {
                resumeMoveIndex = resultIndex;
                waitingForRewind = false;
            }));

            while (waitingForRewind)
            {
                yield return null;
            }
        }

        for (int i = resumeMoveIndex; i < savedMoves.Length; i++)
        {
            if (!isPlaying)
            {
                resumeMoveIndex = i;
                yield break;
            }

            while (tc.IsMoving)
            {
                if (!isPlaying) { resumeMoveIndex = i; yield break; }
                yield return null;
            }

            Vector3 dir = ParseMove(savedMoves[i]);
            if (dir != Vector3.zero)
                tc.TryMoveExternal(dir);

            yield return new WaitForSeconds(moveDelay);
        }

        while (tc.IsMoving) yield return null;
        resumeMoveIndex = 0;
        savedMoves = null;
        Finish(allDone: true);
    }

    private void Finish(bool allDone)
    {
        isPlaying = false;
        if (allDone)
            hintSessionCharged = false;
        RefreshHintCreditsLabel();
    }

    private static Vector3 ParseMove(string move)
    {
        switch (move.ToUpper())
        {
            case "F": return Vector3.forward;
            case "B": return Vector3.back;
            case "L": return Vector3.left;
            case "R": return Vector3.right;
            default: return Vector3.zero;
        }
    }

    public void ResetHintState()
    {
        resumeMoveIndex = 0;
        isPlaying = false;
        hintSessionCharged = false;
        RefreshHintCreditsLabel();
    }
}
