using System;
using System.Collections;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// AdMob yöneticisi: rewarded (hint) + interstitial (seviye geçiş / pause).
/// Varsayılan: Google resmi test ID'leri.
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    public const int InterstitialStartAbsoluteLevel = 5;
    public const int InterstitialEveryNLevels = 3;
    public const int LevelsPerWorld = 20;

    public static class GoogleTestIds
    {
        public const string AndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string IosAppId = "ca-app-pub-3940256099942544~1458002511";
        public const string AndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string IosRewarded = "ca-app-pub-3940256099942544/1712485313";
        public const string AndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string IosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string AndroidBanner = "ca-app-pub-3940256099942544/6300978111";
        public const string IosBanner = "ca-app-pub-3940256099942544/2934735716";
    }

    [Header("Mode")]
    [Tooltip("Açıkken Google resmi test reklam birimleri kullanılır (güvenli).")]
    [SerializeField] private bool useTestAds = false;

    [Tooltip("Editor'da gerçek SDK yerine kısa mock reklam (cihazda AdMob test reklamı çalışır).")]
    [SerializeField] private bool useMockInEditor = true;

    [SerializeField] private float mockAdDurationSeconds = 1.25f;

    [Header("Production Ad Unit IDs (useTestAds kapalıyken)")]
    [SerializeField] private string androidRewardedAdUnitId = "ca-app-pub-5240694796566154/7969851280";
    [SerializeField] private string iosRewardedAdUnitId = "ca-app-pub-5240694796566154/6671981403";
    [SerializeField] private string androidInterstitialAdUnitId = "ca-app-pub-5240694796566154/5494631105";
    [SerializeField] private string iosInterstitialAdUnitId = "ca-app-pub-5240694796566154/7367216053";

    [Header("Reward")]
    [SerializeField] private int hintsGrantedPerAd = 1;

    public bool IsInitialized { get; private set; }
    public bool IsShowing { get; private set; }
    public bool IsRewardedReady => _rewardedAd != null && _rewardedAd.CanShowAd();
    public bool IsInterstitialReady => _interstitialAd != null && _interstitialAd.CanShowAd();
    public int HintsGrantedPerAd => Mathf.Max(1, hintsGrantedPerAd);
    public bool UseTestAds => useTestAds;

    private RewardedAd _rewardedAd;
    private InterstitialAd _interstitialAd;
    private bool _isLoadingRewarded;
    private bool _isLoadingInterstitial;
    private Action<bool> _pendingRewardedCallback;
    private Action _pendingInterstitialCallback;
    private bool _rewardEarned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AdsManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AdsManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(InitializeWhenAttReady());
    }

    /// <summary>ATT uygulama açılışında sorulur; AdMob yalnızca yanıt sonrası init olur.</summary>
    private IEnumerator InitializeWhenAttReady()
    {
        yield return IosAttRequester.WaitUntilComplete();
        Initialize();
    }

    private void OnDestroy()
    {
        DestroyRewarded();
        DestroyInterstitial();
        if (Instance == this)
            Instance = null;
    }

    public void Initialize()
    {
        if (IsInitialized) return;

#if UNITY_EDITOR
        if (useMockInEditor)
        {
            IsInitialized = true;
            Debug.Log("[AdsManager] Editor mock modu — gerçek reklam cihazda gösterilir.");
            return;
        }
#endif

        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(status =>
        {
            IsInitialized = true;
            Debug.Log($"[AdsManager] SDK hazır (testAds={useTestAds}).");
            LoadRewarded();
            LoadInterstitial();
        });
    }

    /// <summary>
    /// Mutlak seviye: (world-1)*20 + level. 5, 8, 11, 14... için true.
    /// </summary>
    public static bool ShouldShowLevelClearInterstitial(int worldIndex, int levelIndex)
    {
        int absolute = GetAbsoluteLevelIndex(worldIndex, levelIndex);
        if (absolute < InterstitialStartAbsoluteLevel)
            return false;
        return (absolute - InterstitialStartAbsoluteLevel) % InterstitialEveryNLevels == 0;
    }

    public static int GetAbsoluteLevelIndex(int worldIndex, int levelIndex)
    {
        int w = Mathf.Max(1, worldIndex);
        int l = Mathf.Clamp(levelIndex, 1, LevelsPerWorld);
        return (w - 1) * LevelsPerWorld + l;
    }

    public string GetRewardedAdUnitId()
    {
        if (useTestAds)
        {
#if UNITY_IOS
            return GoogleTestIds.IosRewarded;
#else
            return GoogleTestIds.AndroidRewarded;
#endif
        }

#if UNITY_IOS
        return string.IsNullOrWhiteSpace(iosRewardedAdUnitId)
            ? GoogleTestIds.IosRewarded
            : iosRewardedAdUnitId.Trim();
#else
        return string.IsNullOrWhiteSpace(androidRewardedAdUnitId)
            ? GoogleTestIds.AndroidRewarded
            : androidRewardedAdUnitId.Trim();
#endif
    }

    public string GetInterstitialAdUnitId()
    {
        if (useTestAds)
        {
#if UNITY_IOS
            return GoogleTestIds.IosInterstitial;
#else
            return GoogleTestIds.AndroidInterstitial;
#endif
        }

#if UNITY_IOS
        return string.IsNullOrWhiteSpace(iosInterstitialAdUnitId)
            ? GoogleTestIds.IosInterstitial
            : iosInterstitialAdUnitId.Trim();
#else
        return string.IsNullOrWhiteSpace(androidInterstitialAdUnitId)
            ? GoogleTestIds.AndroidInterstitial
            : androidInterstitialAdUnitId.Trim();
#endif
    }

    public void LoadRewarded()
    {
#if UNITY_EDITOR
        if (useMockInEditor) return;
#endif
        if (!IsInitialized || _isLoadingRewarded || IsRewardedReady) return;

        _isLoadingRewarded = true;
        string unitId = GetRewardedAdUnitId();
        Debug.Log($"[AdsManager] Rewarded yükleniyor: {unitId}");

        RewardedAd.Load(unitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            _isLoadingRewarded = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdsManager] Rewarded yüklenemedi: {error}");
                return;
            }

            DestroyRewarded();
            _rewardedAd = ad;
            RegisterRewardedEvents(_rewardedAd);
            Debug.Log("[AdsManager] Rewarded hazır.");
        });
    }

    public void LoadInterstitial()
    {
#if UNITY_EDITOR
        if (useMockInEditor) return;
#endif
        if (!IsInitialized || _isLoadingInterstitial || IsInterstitialReady) return;

        _isLoadingInterstitial = true;
        string unitId = GetInterstitialAdUnitId();
        Debug.Log($"[AdsManager] Interstitial yükleniyor: {unitId}");

        InterstitialAd.Load(unitId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
        {
            _isLoadingInterstitial = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdsManager] Interstitial yüklenemedi: {error}");
                return;
            }

            DestroyInterstitial();
            _interstitialAd = ad;
            RegisterInterstitialEvents(_interstitialAd);
            Debug.Log("[AdsManager] Interstitial hazır.");
        });
    }

    /// <summary>Rewarded gösterir. onCompleted(true) = ödül kazanıldı.</summary>
    public void ShowRewarded(Action<bool> onCompleted)
    {
        if (IsShowing)
        {
            onCompleted?.Invoke(false);
            return;
        }

#if UNITY_EDITOR
        if (useMockInEditor)
        {
            StartCoroutine(MockAdRoutine(() => onCompleted?.Invoke(true)));
            return;
        }
#endif

        if (!IsInitialized)
        {
            Debug.LogWarning("[AdsManager] SDK henüz hazır değil.");
            onCompleted?.Invoke(false);
            Initialize();
            return;
        }

        if (!IsRewardedReady)
        {
            Debug.LogWarning("[AdsManager] Rewarded henüz yüklenmedi.");
            LoadRewarded();
            onCompleted?.Invoke(false);
            return;
        }

        _pendingRewardedCallback = onCompleted;
        _rewardEarned = false;
        IsShowing = true;
        _rewardedAd.Show(reward =>
        {
            _rewardEarned = true;
            Debug.Log($"[AdsManager] Ödül: {reward.Type} x {reward.Amount}");
        });
    }

    /// <summary>
    /// Interstitial gösterir; kapanınca veya gösterilemezse onClosed çağrılır (akış bloklanmaz).
    /// </summary>
    public void ShowInterstitial(Action onClosed)
    {
        if (IsShowing)
        {
            onClosed?.Invoke();
            return;
        }

#if UNITY_EDITOR
        if (useMockInEditor)
        {
            StartCoroutine(MockAdRoutine(onClosed));
            return;
        }
#endif

        if (!IsInitialized)
        {
            Initialize();
            onClosed?.Invoke();
            return;
        }

        if (!IsInterstitialReady)
        {
            Debug.LogWarning("[AdsManager] Interstitial hazır değil, atlanıyor.");
            LoadInterstitial();
            onClosed?.Invoke();
            return;
        }

        _pendingInterstitialCallback = onClosed;
        IsShowing = true;
        _interstitialAd.Show();
    }

    /// <summary>Hazırsa interstitial gösterir, değilse hemen devam eder.</summary>
    public void ShowInterstitialThen(Action continueAction)
    {
        if (Instance == null)
        {
            continueAction?.Invoke();
            return;
        }

        ShowInterstitial(continueAction);
    }

    private IEnumerator MockAdRoutine(Action onClosed)
    {
        IsShowing = true;
        Debug.Log($"[AdsManager] Editor mock reklam ({mockAdDurationSeconds:0.00}s)...");
        yield return new WaitForSecondsRealtime(mockAdDurationSeconds);
        IsShowing = false;
        Debug.Log("[AdsManager] Editor mock tamam.");
        onClosed?.Invoke();
    }

    private void RegisterRewardedEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            FinishRewarded(_rewardEarned);
            DestroyRewarded();
            LoadRewarded();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"[AdsManager] Rewarded gösterilemedi: {error}");
            FinishRewarded(false);
            DestroyRewarded();
            LoadRewarded();
        };
    }

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            FinishInterstitial();
            DestroyInterstitial();
            LoadInterstitial();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"[AdsManager] Interstitial gösterilemedi: {error}");
            FinishInterstitial();
            DestroyInterstitial();
            LoadInterstitial();
        };
    }

    private void FinishRewarded(bool success)
    {
        IsShowing = false;
        var cb = _pendingRewardedCallback;
        _pendingRewardedCallback = null;
        cb?.Invoke(success);
    }

    private void FinishInterstitial()
    {
        IsShowing = false;
        var cb = _pendingInterstitialCallback;
        _pendingInterstitialCallback = null;
        cb?.Invoke();
    }

    private void DestroyRewarded()
    {
        if (_rewardedAd == null) return;
        _rewardedAd.Destroy();
        _rewardedAd = null;
    }

    private void DestroyInterstitial()
    {
        if (_interstitialAd == null) return;
        _interstitialAd.Destroy();
        _interstitialAd = null;
    }
}
