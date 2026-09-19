using UnityEngine;

/// <summary>
/// Stretch + middle karışık SF panellerini sabit tasarım boyutunda tutar
/// (telefon düzeni), sonra tablette / dar ekranda tek parça ölçekler.
/// CanvasScaler'a ve child anchor'lara dokunmaz.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class MenuScreenAdapter : MonoBehaviour
{
    public const string FitRootName = "MenuFitRoot";

    [SerializeField] private Vector2 designSize = new Vector2(1280f, 600f);
    [SerializeField] private float maxScale = 1f;
    [SerializeField] private bool useSafeArea = false;

    private RectTransform canvasRect;
    private int lastW;
    private int lastH;
    private Rect lastSafe;
    private bool applying;

    private void Awake()
    {
        canvasRect = transform as RectTransform;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnRectTransformDimensionsChange()
    {
        Apply();
    }

    private void LateUpdate()
    {
        if (Screen.width != lastW || Screen.height != lastH || Screen.safeArea != lastSafe)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (applying)
        {
            return;
        }

        applying = true;
        try
        {
            ApplyInternal();
        }
        finally
        {
            applying = false;
        }
    }

    private void ApplyInternal()
    {
        if (canvasRect == null)
        {
            canvasRect = transform as RectTransform;
        }

        if (canvasRect == null || designSize.x < 1f || designSize.y < 1f)
        {
            return;
        }

        lastW = Screen.width;
        lastH = Screen.height;
        lastSafe = Screen.safeArea;

        float availW = canvasRect.rect.width;
        float availH = canvasRect.rect.height;

        if (useSafeArea && lastW > 0 && lastH > 0)
        {
            Rect safe = Screen.safeArea;
            availW *= Mathf.Clamp01(safe.width / lastW);
            availH *= Mathf.Clamp01(safe.height / lastH);
        }

        float fit = Mathf.Min(availW / designSize.x, availH / designSize.y, maxScale);
        if (fit < 0.05f)
        {
            fit = 0.05f;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            RectTransform fitRoot = child.Find(FitRootName) as RectTransform;
            if (fitRoot == null)
            {
                continue;
            }

            fitRoot.anchorMin = new Vector2(0.5f, 0.5f);
            fitRoot.anchorMax = new Vector2(0.5f, 0.5f);
            fitRoot.pivot = new Vector2(0.5f, 0.5f);
            fitRoot.anchoredPosition3D = Vector3.zero;
            fitRoot.sizeDelta = designSize;
            fitRoot.localScale = Vector3.one * fit;
        }
    }
}
