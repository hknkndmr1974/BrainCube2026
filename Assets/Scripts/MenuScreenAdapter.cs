using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stretch + middle karışık SF panellerini sabit tasarım boyutunda tutar
/// (telefon düzeni), sonra panellerin gerçek kapladığı alanı ölçüp
/// safe area (+ kenar payı) içine sığacak şekilde tek parça ölçekler.
/// CanvasScaler'a ve child anchor'lara dokunmaz.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class MenuScreenAdapter : MonoBehaviour
{
    public const string FitRootName = "MenuFitRoot";

    private const int MaxMeasureDepth = 4;

    [Tooltip("MenuFitRoot'un yerleşim kutusu. Paneller bu kutuya göre esner.")]
    [SerializeField] private Vector2 designSize = new Vector2(1280f, 600f);
    [SerializeField] private float maxScale = 1f;
    [SerializeField] private bool useSafeArea = true;
    [Tooltip("Safe area kenarlarından bırakılan pay (kısa kenarın oranı). iPad'in yuvarlak köşeleri safe area'ya dahil değildir.")]
    [Range(0f, 0.15f)]
    [SerializeField] private float edgeMargin = 0.04f;
    [Tooltip("Açık: tüm menüler en zor sığanın ölçeğini kullanır, yazı/buton boyutları menüler arasında aynı kalır.")]
    [SerializeField] private bool sharedScale = true;

    private readonly List<RectTransform> fitRoots = new List<RectTransform>();
    private readonly List<Vector2> fitCenters = new List<Vector2>();
    private readonly List<float> fitScales = new List<float>();

    private RectTransform canvasRect;
    private int lastW;
    private int lastH;
    private Rect lastSafe;
    private bool applying;
    private bool settingsChanged;

    private void Awake()
    {
        canvasRect = transform as RectTransform;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        settingsChanged = true;
    }

    private void OnRectTransformDimensionsChange()
    {
        Apply();
    }

    private void LateUpdate()
    {
        if (settingsChanged || Screen.width != lastW || Screen.height != lastH || Screen.safeArea != lastSafe)
        {
            settingsChanged = false;
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

        fitRoots.Clear();
        fitCenters.Clear();
        fitScales.Clear();
        float sharedFit = float.MaxValue;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            RectTransform fitRoot = child.Find(FitRootName) as RectTransform;
            if (fitRoot == null)
            {
                continue;
            }

            RectTransform area = fitRoot.parent as RectTransform;
            Vector2 areaSize = area != null ? area.rect.size : canvasRect.rect.size;

            Vector2 availSize = areaSize;
            Vector2 center = Vector2.zero;
            if (useSafeArea && lastW > 0 && lastH > 0)
            {
                Rect safe = lastSafe;
                availSize = new Vector2(
                    areaSize.x * Mathf.Clamp01(safe.width / lastW),
                    areaSize.y * Mathf.Clamp01(safe.height / lastH));
                center = new Vector2(
                    (safe.center.x / lastW - 0.5f) * areaSize.x,
                    (safe.center.y / lastH - 0.5f) * areaSize.y);
            }

            float margin = edgeMargin * Mathf.Min(availSize.x, availSize.y);
            availSize -= new Vector2(margin * 2f, margin * 2f);

            fitRoot.anchorMin = new Vector2(0.5f, 0.5f);
            fitRoot.anchorMax = new Vector2(0.5f, 0.5f);
            fitRoot.pivot = new Vector2(0.5f, 0.5f);
            fitRoot.sizeDelta = designSize;

            Vector2 halfExtents = MeasureHalfExtents(fitRoot);
            float fit = Mathf.Min(
                availSize.x / (halfExtents.x * 2f),
                availSize.y / (halfExtents.y * 2f),
                maxScale);
            if (fit < 0.05f)
            {
                fit = 0.05f;
            }

            fitRoots.Add(fitRoot);
            fitCenters.Add(center);
            fitScales.Add(fit);
            sharedFit = Mathf.Min(sharedFit, fit);
        }

        for (int i = 0; i < fitRoots.Count; i++)
        {
            float fit = sharedScale ? sharedFit : fitScales[i];
            fitRoots[i].anchoredPosition3D = new Vector3(fitCenters[i].x, fitCenters[i].y, 0f);
            fitRoots[i].localScale = Vector3.one * fit;
        }
    }

    /// <summary>
    /// Panellerin açık haldeki yerleşimini fitRoot merkezine göre ölçer ve
    /// merkezden en uzak kenarları döndürür (fitRoot ortada kalsın diye simetrik).
    /// </summary>
    private Vector2 MeasureHalfExtents(RectTransform fitRoot)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        AccumulateChildren(fitRoot, Vector2.zero, Vector2.one, 1, ref min, ref max);

        if (min.x > max.x || min.y > max.y)
        {
            return designSize * 0.5f;
        }

        return new Vector2(
            Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x), 1f),
            Mathf.Max(Mathf.Abs(min.y), Mathf.Abs(max.y), 1f));
    }

    private static void AccumulateChildren(RectTransform parent, Vector2 parentOrigin, Vector2 parentScale, int depth,
        ref Vector2 min, ref Vector2 max)
    {
        Rect parentRect = parent.rect;
        for (int i = 0; i < parent.childCount; i++)
        {
            RectTransform child = parent.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            // Kapanış animasyonları pencereleri anchoredPosition ile ekran dışına iter;
            // açık halde hepsi 0 olduğu için ölçümde anchoredPosition yok sayılır.
            Vector2 anchorPoint = new Vector2(
                Mathf.Lerp(child.anchorMin.x, child.anchorMax.x, child.pivot.x),
                Mathf.Lerp(child.anchorMin.y, child.anchorMax.y, child.pivot.y));
            Vector2 anchorRef = parentRect.min + Vector2.Scale(anchorPoint, parentRect.size);
            Vector2 origin = parentOrigin + Vector2.Scale(parentScale, anchorRef);

            Vector3 ls = child.localScale;
            Vector2 scale = Vector2.Scale(parentScale, new Vector2(RestScale(ls.x), RestScale(ls.y)));

            bool isLeaf = HasVisibleGraphic(child) || depth >= MaxMeasureDepth || !HasActiveRectChild(child);
            if (isLeaf)
            {
                Rect r = child.rect;
                Vector2 a = origin + Vector2.Scale(scale, r.min);
                Vector2 b = origin + Vector2.Scale(scale, r.max);
                min = Vector2.Min(min, Vector2.Min(a, b));
                max = Vector2.Max(max, Vector2.Max(a, b));
            }
            else
            {
                AccumulateChildren(child, origin, scale, depth + 1, ref min, ref max);
            }
        }
    }

    private static float RestScale(float s)
    {
        s = Mathf.Abs(s);
        return s < 0.5f ? 1f : s;
    }

    private static bool HasVisibleGraphic(Transform t)
    {
        Graphic g = t.GetComponent<Graphic>();
        return g != null && g.enabled;
    }

    private static bool HasActiveRectChild(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            if (c.gameObject.activeSelf && c is RectTransform)
            {
                return true;
            }
        }

        return false;
    }
}
