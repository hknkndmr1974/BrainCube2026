using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main menu arka planında farklı boyut ve hızlarda dönen blok alanı.
/// Bloklar ekran (viewport) koordinatlarında saklanır ve kameranın görüşüne göre
/// konumlanır; böylece her ekran oranında hepsi görünür, panelin arkası boş kalır.
/// </summary>
public class MenuCubeField : MonoBehaviour
{
    [SerializeField] private GameObject cubeTemplate;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Material[] palette;
    [SerializeField] private int cubeCount = 22;
    [SerializeField] private int seed = 42;
    [Tooltip("Kameradan uzaklık aralığı (dünya birimi).")]
    [SerializeField] private Vector2 depthRange = new Vector2(42f, 100f);
    [SerializeField] private Vector2 scaleRange = new Vector2(0.35f, 0.75f);
    [Tooltip("Ekran ortasında boş bırakılan yarı genişlik (viewport oranı). Menü paneli burada durur.")]
    [SerializeField] [Range(0f, 0.45f)] private float centerClear = 0.22f;
    [Tooltip("Kenar payları ve aralıklar bu orana göre hesaplanır; daha geniş ekranlarda bloklar sadece açılır.")]
    [SerializeField] private float referenceAspect = 4f / 3f;
    [SerializeField] [Range(0f, 0.2f)] private float edgePadding = 0.03f;
    [SerializeField] private Vector2 spinYRange = new Vector2(6f, 30f);
    [SerializeField] private Vector2 spinXZRange = new Vector2(2f, 10f);

    [SerializeField] [HideInInspector] private List<Transform> cubes = new List<Transform>();
    [SerializeField] [HideInInspector] private List<Vector3> anchors = new List<Vector3>();

    private float lastAspect;

    private void Start()
    {
        Reposition();
    }

    private void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam != null && !Mathf.Approximately(cam.aspect, lastAspect))
        {
            Reposition();
        }
    }

    [ContextMenu("Rebuild Field")]
    public void RebuildField()
    {
        Camera cam = ResolveCamera();
        if (cubeTemplate == null || cam == null)
        {
            Debug.LogWarning("MenuCubeField: cubeTemplate veya kamera atanmamış.");
            return;
        }

        ClearGenerated();

        var rng = new System.Random(seed);
        var colorRng = new System.Random(seed * 31 + 7);
        int lastColor = -1;
        Vector3 baseScale = cubeTemplate.transform.localScale;
        float diagonal = baseScale.magnitude;
        float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        var placedPos = new List<Vector2>();
        var placedRadius = new List<float>();

        for (int i = 0; i < cubeCount; i++)
        {
            float depth01 = Mathf.Pow((float)rng.NextDouble(), 0.8f);
            float depth = Mathf.Lerp(depthRange.x, depthRange.y, depth01);
            float scaleMul = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());

            // Dönen bloğun ekranda kaplayabileceği en büyük yarıçap (viewport yüksekliği oranı).
            float radiusV = (diagonal * scaleMul * 0.5f) / (2f * tanHalf * depth);
            float radiusU = radiusV / referenceAspect;

            float side = (i % 2 == 0) ? -1f : 1f;
            float uMin = 0.5f + centerClear + radiusU;
            float uMax = 1f - edgePadding - radiusU;
            float vMin = edgePadding + radiusV;
            float vMax = 1f - edgePadding - radiusV;
            if (uMin >= uMax || vMin >= vMax)
            {
                continue;
            }

            bool found = false;
            Vector2 uv = Vector2.zero;
            for (int attempt = 0; attempt < 40 && !found; attempt++)
            {
                float u = Mathf.Lerp(uMin, uMax, (float)rng.NextDouble());
                if (side < 0f)
                {
                    u = 1f - u;
                }

                uv = new Vector2(u, Mathf.Lerp(vMin, vMax, (float)rng.NextDouble()));
                found = true;
                for (int p = 0; p < placedPos.Count; p++)
                {
                    Vector2 d = uv - placedPos[p];
                    d.x *= referenceAspect;
                    if (d.magnitude < (radiusV + placedRadius[p]) * 1.05f)
                    {
                        found = false;
                        break;
                    }
                }
            }

            if (!found)
            {
                continue;
            }

            placedPos.Add(uv);
            placedRadius.Add(radiusV);

            var go = Instantiate(cubeTemplate, transform);
            go.name = $"DisplayCube_{i:00}";
            go.SetActive(true);
            go.transform.localScale = baseScale * scaleMul;
            go.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f);

            if (palette != null && palette.Length > 0)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    int color = colorRng.Next(palette.Length);
                    if (color == lastColor && palette.Length > 1)
                    {
                        color = (color + 1) % palette.Length;
                    }

                    lastColor = color;
                    mr.sharedMaterial = palette[color];
                }
            }

            float spinSign = rng.NextDouble() < 0.5 ? -1f : 1f;
            float spinSpeed = Mathf.Lerp(spinYRange.x, spinYRange.y, (float)rng.NextDouble());
            float spinX = Mathf.Lerp(spinXZRange.x, spinXZRange.y, (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            float spinZ = Mathf.Lerp(spinXZRange.x, spinXZRange.y, (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1f : 1f);

            var spinner = go.GetComponent<MenuCubeSpinner>();
            if (spinner == null) spinner = go.AddComponent<MenuCubeSpinner>();
            spinner.SetDegreesPerSecond(new Vector3(spinX, spinSign * spinSpeed, spinZ));

            cubes.Add(go.transform);
            anchors.Add(new Vector3(uv.x, uv.y, depth));
        }

        // Template sahne önizlemesinde görünmesin
        cubeTemplate.SetActive(false);
        Reposition();
    }

    [ContextMenu("Clear Generated")]
    public void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (cubeTemplate != null && child.gameObject == cubeTemplate) continue;
            if (!child.name.StartsWith("DisplayCube_")) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
#endif
                Destroy(child.gameObject);
        }

        cubes.Clear();
        anchors.Clear();
    }

    private void Reposition()
    {
        Camera cam = ResolveCamera();
        if (cam == null)
        {
            return;
        }

        lastAspect = cam.aspect;
        int count = Mathf.Min(cubes.Count, anchors.Count);
        for (int i = 0; i < count; i++)
        {
            if (cubes[i] != null)
            {
                cubes[i].position = cam.ViewportToWorldPoint(anchors[i]);
            }
        }
    }

    private Camera ResolveCamera()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        return viewCamera;
    }
}
