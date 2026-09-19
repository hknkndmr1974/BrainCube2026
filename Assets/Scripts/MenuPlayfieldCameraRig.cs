using UnityEngine;

/// <summary>
/// Main Camera (3D playfield). GUI Camera'ya, tile/küp transformuna dokunmaz.
/// Tablette yazı kesilmeden ekrana sığacak kadar kamerayı geri çeker;
/// telefon mesafesine mümkün olduğunca yakın kalır.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class MenuPlayfieldCameraRig : MonoBehaviour
{
    [SerializeField] private Camera playfieldCamera;
    [SerializeField] private float phoneAspect = 16f / 9f;
    [Tooltip("Yazının kaplaması gereken genişlik (1 = tam ekran, taşma yok).")]
    [SerializeField] [Range(0.9f, 1f)] private float fitWidth = 0.98f;
    [SerializeField] [Range(0.9f, 1f)] private float fitHeight = 0.98f;
    [SerializeField] private float maxDolly = 80f;

    private Vector3 defaultPos;
    private Quaternion defaultRot;
    private float defaultFov;
    private bool defaultsStored;
    private int lastW;
    private int lastH;
    private int retryFrames;

    private void Awake()
    {
        if (playfieldCamera == null)
        {
            playfieldCamera = GetComponent<Camera>();
        }

        StoreDefaults();
    }

    private void OnEnable()
    {
        StoreDefaults();
        retryFrames = 8;
        Apply();
    }

    private void Start()
    {
        retryFrames = 8;
        Apply();
    }

    private void LateUpdate()
    {
        if (playfieldCamera == null)
        {
            return;
        }

        bool sizeChanged = playfieldCamera.pixelWidth != lastW || playfieldCamera.pixelHeight != lastH;
        if (sizeChanged || retryFrames > 0)
        {
            if (retryFrames > 0)
            {
                retryFrames--;
            }

            Apply();
        }
    }

    private void OnDisable()
    {
        Restore();
    }

    private void StoreDefaults()
    {
        if (defaultsStored || playfieldCamera == null)
        {
            return;
        }

        defaultPos = playfieldCamera.transform.position;
        defaultRot = playfieldCamera.transform.rotation;
        defaultFov = playfieldCamera.fieldOfView;
        defaultsStored = true;
    }

    private void Restore()
    {
        if (!defaultsStored || playfieldCamera == null)
        {
            return;
        }

        playfieldCamera.transform.position = defaultPos;
        playfieldCamera.transform.rotation = defaultRot;
        playfieldCamera.fieldOfView = defaultFov;
    }

    private void Apply()
    {
        if (playfieldCamera == null)
        {
            return;
        }

        StoreDefaults();
        Restore();

        lastW = playfieldCamera.pixelWidth;
        lastH = Mathf.Max(1, playfieldCamera.pixelHeight);
        float aspect = (float)lastW / lastH;
        if (aspect >= phoneAspect - 0.03f)
        {
            return;
        }

        Rect vp;
        if (!TryGetLetterViewport(playfieldCamera, out vp) || vp.width < 0.05f || vp.height < 0.05f)
        {
            return;
        }

        // Sadece taşanı sığdır — gerekenden fazla uzaklaşma.
        float pullW = vp.width / fitWidth;
        float pullH = vp.height / fitHeight;
        float pull = Mathf.Max(pullW, pullH, 1f);
        if (pull <= 1.005f)
        {
            return;
        }

        MenuPlayfield field = FindFirstObjectByType<MenuPlayfield>();
        if (field == null)
        {
            return;
        }

        Transform tiles = field.transform.Find("Tiles");
        if (tiles == null || tiles.childCount == 0)
        {
            return;
        }

        Vector3 center = GetLetterCenter(tiles);
        float depth = Vector3.Dot(center - defaultPos, playfieldCamera.transform.forward);
        if (depth < 1f)
        {
            return;
        }

        float extra = Mathf.Clamp(depth * (pull - 1f), 0f, maxDolly);
        playfieldCamera.transform.position = defaultPos - playfieldCamera.transform.forward * extra;
    }

    private static bool TryGetLetterViewport(Camera cam, out Rect rect)
    {
        rect = default(Rect);
        MenuPlayfield field = FindFirstObjectByType<MenuPlayfield>();
        if (field == null)
        {
            return false;
        }

        Transform tiles = field.transform.Find("Tiles");
        if (tiles == null || tiles.childCount == 0)
        {
            return false;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        bool any = false;

        // Dünya AABB değil: her tile merkezini yansıt (döndürülmüş kutu şişirmez).
        for (int i = 0; i < tiles.childCount; i++)
        {
            Vector3 vp = cam.WorldToViewportPoint(tiles.GetChild(i).position);
            if (vp.z <= 0f)
            {
                continue;
            }

            any = true;
            if (vp.x < minX) minX = vp.x;
            if (vp.x > maxX) maxX = vp.x;
            if (vp.y < minY) minY = vp.y;
            if (vp.y > maxY) maxY = vp.y;
        }

        if (!any)
        {
            return false;
        }

        // Tile boyutu için küçük pay (~yarım tile)
        const float pad = 0.012f;
        rect = Rect.MinMaxRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
        return true;
    }

    private static Vector3 GetLetterCenter(Transform tiles)
    {
        Vector3 sum = Vector3.zero;
        int n = tiles.childCount;
        for (int i = 0; i < n; i++)
        {
            sum += tiles.GetChild(i).position;
        }

        return n > 0 ? sum / n : tiles.position;
    }
}
