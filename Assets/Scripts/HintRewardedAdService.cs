using System;
using UnityEngine;

/// <summary>
/// Hint için rewarded reklam kapısı.
/// Gerçek gösterim <see cref="AdsManager"/> (AdMob test/production) üzerinden yapılır.
/// </summary>
public class HintRewardedAdService : MonoBehaviour
{
    public static HintRewardedAdService Instance { get; private set; }

    [Tooltip("Başarılı reklam sonrası verilecek hint hakkı (AdsManager yoksa kullanılır).")]
    [SerializeField] private int hintsGrantedPerAd = 1;

    public bool IsShowing => AdsManager.Instance != null && AdsManager.Instance.IsShowing;

    public int HintsGrantedPerAd
    {
        get
        {
            if (AdsManager.Instance != null)
                return AdsManager.Instance.HintsGrantedPerAd;
            return Mathf.Max(1, hintsGrantedPerAd);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Rewarded reklam gösterir. onCompleted(success): başarıda true.
    /// </summary>
    public void ShowRewarded(Action<bool> onCompleted)
    {
        if (AdsManager.Instance == null)
        {
            Debug.LogWarning("[HintRewardedAdService] AdsManager yok.");
            onCompleted?.Invoke(false);
            return;
        }

        AdsManager.Instance.ShowRewarded(onCompleted);
    }
}
