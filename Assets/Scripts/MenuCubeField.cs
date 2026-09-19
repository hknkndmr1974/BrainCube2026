using UnityEngine;

/// <summary>
/// Main menu arka planında derinlik hissi veren, farklı boyut ve hızlarda dönen küp alanı.
/// </summary>
public class MenuCubeField : MonoBehaviour
{
    [SerializeField] private GameObject cubeTemplate;
    [SerializeField] private int cubeCount = 20;
    [SerializeField] private int seed = 42;
    [SerializeField] private float centerClearX = 14f;
    [SerializeField] private Vector2 xRange = new Vector2(-48f, 48f);
    [SerializeField] private Vector2 yRange = new Vector2(-10f, 12f);
    [SerializeField] private Vector2 zRange = new Vector2(-18f, 36f);
    [SerializeField] private Vector2 scaleRange = new Vector2(0.22f, 1.05f);
    [SerializeField] private Vector2 spinYRange = new Vector2(6f, 38f);
    [SerializeField] private Vector2 spinXZRange = new Vector2(2f, 10f);

    [ContextMenu("Rebuild Field")]
    public void RebuildField()
    {
        if (cubeTemplate == null)
        {
            Debug.LogWarning("MenuCubeField: cubeTemplate atanmamış.");
            return;
        }

        ClearGenerated();

        var rng = new System.Random(seed);
        var baseScale = cubeTemplate.transform.localScale;

        for (int i = 0; i < cubeCount; i++)
        {
            // Derinlik: uzak = küçük + yavaş, yakın = büyük + biraz daha hızlı
            float depth01 = Mathf.Clamp01((float)rng.NextDouble());
            // Karışık dağılım; arka planda biraz daha çok küçük küp
            depth01 = Mathf.Pow(depth01, 0.75f);

            float z = Mathf.Lerp(zRange.x, zRange.y, depth01);
            float nearness = 1f - depth01; // 1 = kameraya yakın

            float scaleMul = Mathf.Lerp(scaleRange.x, scaleRange.y, nearness * 0.65f + (float)rng.NextDouble() * 0.35f);
            // Çok yakındakiler aşırı büyük olmasın
            if (z < -8f) scaleMul = Mathf.Min(scaleMul, 0.85f);

            // Sağ/sol dengesi: sırayla taraf, sonra o tarafta rastgele X
            float side = (i % 2 == 0) ? -1f : 1f;
            float clear = Mathf.Lerp(centerClearX * 0.4f, centerClearX, nearness);
            float xMin = clear;
            float xMax = Mathf.Abs(side < 0 ? xRange.x : xRange.y);
            float x = side * Mathf.Lerp(xMin, xMax, (float)rng.NextDouble());

            // Uzak küçük küpler hafif ortaya kayabilir (derinlik hissi)
            if (nearness < 0.35f && rng.NextDouble() < 0.35)
                x *= Mathf.Lerp(0.35f, 0.7f, (float)rng.NextDouble());

            float y = Mathf.Lerp(yRange.x, yRange.y, (float)rng.NextDouble());

            var go = Instantiate(cubeTemplate, transform);
            go.name = $"DisplayCube_{i:00}";
            go.SetActive(true);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localScale = baseScale * scaleMul;
            go.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f);

            float spinSign = rng.NextDouble() < 0.5 ? -1f : 1f;
            float spinSpeed = Mathf.Lerp(spinYRange.x, spinYRange.y, nearness * 0.5f + (float)rng.NextDouble() * 0.5f);
            float spinX = Mathf.Lerp(spinXZRange.x, spinXZRange.y, (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            float spinZ = Mathf.Lerp(spinXZRange.x, spinXZRange.y, (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1f : 1f);

            var spinner = go.GetComponent<MenuCubeSpinner>();
            if (spinner == null) spinner = go.AddComponent<MenuCubeSpinner>();
            spinner.SetDegreesPerSecond(new Vector3(spinX, spinSign * spinSpeed, spinZ));
        }

        // Template sahne önizlemesinde görünmesin
        cubeTemplate.SetActive(false);
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
    }
}
