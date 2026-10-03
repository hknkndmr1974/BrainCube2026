using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun kamerasının arkasına, o anki dünyaya göre renklenen gradyan bir zemin koyar.
/// Dünya değişince (aynı sahnede sonraki dünyaya geçiş) rengi kendiliğinden günceller.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameBackground : MonoBehaviour
{
    [System.Serializable]
    public struct WorldGradient
    {
        public Color top;
        public Color bottom;
    }

    [Tooltip("Sırasıyla World 1, 2, 3... Dünya sayısı listeden fazlaysa liste başa sarar.")]
    [SerializeField] private WorldGradient[] worldGradients =
    {
        new WorldGradient { top = new Color32(26, 120, 130, 255), bottom = new Color32(22, 30, 72, 255) },
        new WorldGradient { top = new Color32(170, 82, 62, 255), bottom = new Color32(40, 24, 70, 255) },
        new WorldGradient { top = new Color32(40, 122, 82, 255), bottom = new Color32(16, 40, 44, 255) },
        new WorldGradient { top = new Color32(112, 72, 170, 255), bottom = new Color32(30, 20, 62, 255) },
        new WorldGradient { top = new Color32(30, 100, 170, 255), bottom = new Color32(14, 24, 60, 255) },
        new WorldGradient { top = new Color32(160, 60, 112, 255), bottom = new Color32(40, 18, 52, 255) },
        new WorldGradient { top = new Color32(170, 120, 40, 255), bottom = new Color32(50, 30, 22, 255) },
        new WorldGradient { top = new Color32(40, 150, 150, 255), bottom = new Color32(18, 40, 62, 255) },
        new WorldGradient { top = new Color32(150, 42, 62, 255), bottom = new Color32(36, 14, 32, 255) },
        new WorldGradient { top = new Color32(62, 82, 180, 255), bottom = new Color32(20, 18, 62, 255) },
        new WorldGradient { top = new Color32(110, 140, 52, 255), bottom = new Color32(26, 40, 26, 255) },
        new WorldGradient { top = new Color32(190, 92, 82, 255), bottom = new Color32(50, 24, 42, 255) },
        new WorldGradient { top = new Color32(90, 150, 190, 255), bottom = new Color32(24, 40, 72, 255) },
        new WorldGradient { top = new Color32(122, 52, 122, 255), bottom = new Color32(30, 16, 42, 255) },
        new WorldGradient { top = new Color32(200, 140, 62, 255), bottom = new Color32(30, 40, 72, 255) },
    };

    [Tooltip("Kameraya uzaklık; far clip plane'den küçük olmalı.")]
    [SerializeField] private float planeDistance = 900f;
    [SerializeField] [Range(0f, 0.5f)] private float vignette = 0.22f;

    private const int TextureSize = 128;

    private readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();
    private Camera cam;
    private RawImage image;
    private int shownWorld = int.MinValue;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        BuildCanvas();
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (CurrentWorld() != shownWorld)
        {
            Refresh();
        }
    }

    private void OnDestroy()
    {
        foreach (Texture2D tex in cache.Values)
        {
            if (tex != null)
            {
                Destroy(tex);
            }
        }

        cache.Clear();
    }

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("GameBackgroundCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.layer = gameObject.layer;
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = Mathf.Min(planeDistance, cam.farClipPlane * 0.95f);
        canvas.sortingOrder = -100;

        var imageGo = new GameObject("Gradient", typeof(RectTransform), typeof(RawImage));
        imageGo.layer = gameObject.layer;
        imageGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = (RectTransform)imageGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        image = imageGo.GetComponent<RawImage>();
        image.raycastTarget = false;
    }

    private void Refresh()
    {
        shownWorld = CurrentWorld();
        if (worldGradients == null || worldGradients.Length == 0 || image == null)
        {
            return;
        }

        int index = ((Mathf.Max(shownWorld, 1) - 1) % worldGradients.Length + worldGradients.Length) % worldGradients.Length;
        if (!cache.TryGetValue(index, out Texture2D tex) || tex == null)
        {
            tex = CreateGradient(worldGradients[index]);
            cache[index] = tex;
        }

        image.texture = tex;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = worldGradients[index].bottom;
    }

    private static int CurrentWorld()
    {
        return LevelLoader.Instance != null ? LevelLoader.Instance.worldIndex : 1;
    }

    private Texture2D CreateGradient(WorldGradient g)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "GameBackgroundGradient"
        };

        Color mid = Color.Lerp(g.top, g.bottom, 0.5f);
        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            float v = y / (float)(TextureSize - 1);
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)(TextureSize - 1);
                float t = Mathf.Clamp01((u * 0.45f + (1f - v) * 0.75f) / 1.2f);
                Color c = t < 0.5f ? Color.Lerp(g.top, mid, t / 0.5f) : Color.Lerp(mid, g.bottom, (t - 0.5f) / 0.5f);

                float dx = u - 0.5f;
                float dy = v - 0.5f;
                c *= 1f - vignette * Mathf.Clamp01((dx * dx + dy * dy) * 2.2f);
                c.a = 1f;
                pixels[y * TextureSize + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
