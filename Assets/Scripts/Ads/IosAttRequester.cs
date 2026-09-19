using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// iOS ATT — uygulama ilk açılışında (NotDetermined) sistem diyaloğu.
/// AdMob/reklamdan bağımsız; BeforeSceneLoad'da tetiklenir.
/// Metin: GoogleMobileAdsSettings → User Tracking Usage Description
/// </summary>
public static class IosAttRequester
{
    private const float TimeoutSeconds = 60f;
    private const int NotDetermined = 0;

    private static bool _complete;

    public static bool IsComplete => _complete;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int BrainCube_AttGetStatus();

    [DllImport("__Internal")]
    private static extern void BrainCube_AttRequest();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapOnAppLaunch()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (FindExistingRunner() != null)
        {
            return;
        }

        var go = new GameObject("IosAttBootstrap");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<IosAttBootstrapRunner>();
#else
        _complete = true;
#endif
    }

    public static IEnumerator WaitUntilComplete()
    {
        while (!_complete)
        {
            yield return null;
        }
    }

    internal static void MarkComplete()
    {
        _complete = true;
    }

    private static IosAttBootstrapRunner FindExistingRunner()
    {
        return Object.FindFirstObjectByType<IosAttBootstrapRunner>();
    }

    internal static IEnumerator RequestOnFirstLaunchIfNeeded()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (BrainCube_AttGetStatus() != NotDetermined)
        {
            yield break;
        }

        BrainCube_AttRequest();

        float elapsed = 0f;
        while (elapsed < TimeoutSeconds && BrainCube_AttGetStatus() == NotDetermined)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
#else
        yield break;
#endif
    }
}

internal sealed class IosAttBootstrapRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return IosAttRequester.RequestOnFirstLaunchIfNeeded();
        IosAttRequester.MarkComplete();
        Destroy(gameObject);
    }
}
