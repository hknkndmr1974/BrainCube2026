using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Seçili küp temasını PlayerPrefs'te tutar ve sahnedeki oyuncu/menü küplerine uygular.
/// Material listesi: Resources/CubeThemeCatalog
/// </summary>
public sealed class CubeThemeManager : MonoBehaviour
{
    public const string PrefSelectedThemeIndex = "CubeThemeIndex";
    private const string CatalogResourcePath = "CubeThemeCatalog";

    public static CubeThemeManager Instance { get; private set; }

    public static event Action ThemeChanged;

    public CubeThemeCatalog Catalog { get; private set; }

    public int SelectedIndex
    {
        get
        {
            int fallback = Catalog != null ? Catalog.defaultThemeIndex : 0;
            return Mathf.Clamp(PlayerPrefs.GetInt(PrefSelectedThemeIndex, fallback), 0, Mathf.Max(0, ThemeCount - 1));
        }
        private set
        {
            PlayerPrefs.SetInt(PrefSelectedThemeIndex, value);
            PlayerPrefs.Save();
        }
    }

    public int ThemeCount => Catalog != null ? Catalog.Count : 0;

    private readonly Dictionary<int, Material[]> originalMaterials = new Dictionary<int, Material[]>();
    private readonly Dictionary<int, Mesh> originalMeshes = new Dictionary<int, Mesh>();
    private readonly Dictionary<int, Material[]> originalTileMaterials = new Dictionary<int, Material[]>();
    private readonly Dictionary<int, bool> originalTileVisibility = new Dictionary<int, bool>();
    private readonly Dictionary<int, Material[]> originalGlassMaterials = new Dictionary<int, Material[]>();
    private readonly Dictionary<int, Vector2> originalGlassSizes = new Dictionary<int, Vector2>();

    public const string SkinMeshName = "SkinMesh";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject(nameof(CubeThemeManager));
        go.AddComponent<CubeThemeManager>();
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
        Catalog = Resources.Load<CubeThemeCatalog>(CatalogResourcePath);
        if (Catalog == null)
        {
            Debug.LogWarning("[CubeTheme] Resources/CubeThemeCatalog bulunamadı. Theme menüsü boş kalır.");
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToAllInScene();
    }

    public CubeThemeEntry GetSelectedEntry()
    {
        return Catalog != null ? Catalog.Get(SelectedIndex) : new CubeThemeEntry { displayName = "Default" };
    }

    public Material GetSelectedMaterial()
    {
        return GetSelectedEntry().material;
    }

    public void SelectTheme(int index)
    {
        if (Catalog == null || Catalog.Count == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, Catalog.Count - 1);
        if (SelectedIndex == index && PlayerPrefs.HasKey(PrefSelectedThemeIndex))
        {
            ApplyToAllInScene();
            ThemeChanged?.Invoke();
            return;
        }

        SelectedIndex = index;
        ApplyToAllInScene();
        ThemeChanged?.Invoke();
    }

    public void ApplyTo(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        CubeThemeEntry entry = GetSelectedEntry();
        Material selected = entry.material;
        ApplyMesh(renderers, entry.mesh);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            // Physics material değil; sadece görsel mesh renderer'lar
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                continue;
            }

            int id = renderer.GetInstanceID();
            if (!originalMaterials.ContainsKey(id))
            {
                Material[] shared = renderer.sharedMaterials;
                Material[] copy = new Material[shared.Length];
                for (int m = 0; m < shared.Length; m++)
                {
                    copy[m] = shared[m];
                }

                originalMaterials[id] = copy;
            }

            if (selected == null)
            {
                renderer.sharedMaterials = originalMaterials[id];
            }
            else
            {
                Material[] originals = originalMaterials[id];
                Material[] applied = new Material[originals.Length];
                for (int m = 0; m < applied.Length; m++)
                {
                    applied[m] = selected;
                }

                renderer.sharedMaterials = applied;
            }
        }
    }

    private void ApplyMesh(Renderer[] renderers, Mesh themeMesh)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!(renderers[i] is MeshRenderer))
            {
                continue;
            }

            MeshFilter filter = renderers[i].GetComponent<MeshFilter>();
            if (filter == null)
            {
                continue;
            }

            int id = filter.GetInstanceID();
            if (!originalMeshes.ContainsKey(id))
            {
                originalMeshes[id] = filter.sharedMesh;
            }

            filter.sharedMesh = themeMesh != null ? themeMesh : originalMeshes[id];
        }
    }

    public void ApplyTileSkin(GameObject tile)
    {
        if (tile == null)
        {
            return;
        }

        TileSkin skin = GetSelectedEntry().tileSkin;
        ApplyGlassSkin(tile, skin);
        TileSkin.Rule shape = skin != null ? skin.FindShapeRule(tile.name) : null;
        Transform skinMesh = tile.transform.Find(SkinMeshName);
        BridgeController bridge = tile.GetComponent<BridgeController>();
        if (bridge != null)
        {
            bridge.UsesSkinMesh = shape != null;
        }

        if (shape != null)
        {
            ShowSkinMesh(tile, ref skinMesh, shape);
            if (bridge != null)
            {
                skinMesh.gameObject.SetActive(bridge.IsActive());
            }
        }
        else if (skinMesh != null)
        {
            skinMesh.gameObject.SetActive(false);
        }

        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            bool isMesh = renderer is MeshRenderer;
            if ((!isMesh && !(renderer is SpriteRenderer)) || (skinMesh != null && renderer.transform == skinMesh))
            {
                continue;
            }

            int id = renderer.GetInstanceID();
            if (shape != null)
            {
                if (!originalTileVisibility.ContainsKey(id))
                {
                    originalTileVisibility[id] = renderer.enabled;
                }

                renderer.enabled = false;
            }
            else if (originalTileVisibility.TryGetValue(id, out bool visible))
            {
                bool bridgeHidden = bridge != null && renderer.gameObject == tile && !bridge.IsActive();
                renderer.enabled = visible && !bridgeHidden;
                originalTileVisibility.Remove(id);
            }

            if (!isMesh)
            {
                continue;
            }

            if (!originalTileMaterials.TryGetValue(id, out Material[] originals))
            {
                originals = renderer.sharedMaterials;
                originalTileMaterials[id] = originals;
            }

            Material[] applied = new Material[originals.Length];
            for (int m = 0; m < originals.Length; m++)
            {
                applied[m] = skin != null ? skin.Resolve(tile.name, originals[m]) : originals[m];
            }

            renderer.sharedMaterials = applied;
        }
    }

    /// <summary>FlexibleGlass parçalarını Start'ta kendi malzeme alanlarından üretir; bu yüzden alanları da değiştiriyoruz.</summary>
    private void ApplyGlassSkin(GameObject tile, TileSkin skin)
    {
        var glass = tile.GetComponent<FlexibleGlassDestructor.FlexibleGlass>();
        if (glass == null)
        {
            return;
        }

        int id = glass.GetInstanceID();
        if (!originalGlassMaterials.TryGetValue(id, out Material[] originals))
        {
            originals = new[] { glass.glassMaterial, glass.glassCrossSectionMaterial };
            originalGlassMaterials[id] = originals;
        }

        if (!originalGlassSizes.TryGetValue(id, out Vector2 originalSize))
        {
            originalSize = glass.glassSize;
            originalGlassSizes[id] = originalSize;
        }

        glass.glassMaterial = skin != null ? skin.Resolve(tile.name, originals[0]) : originals[0];
        glass.glassCrossSectionMaterial = skin != null ? skin.Resolve(tile.name, originals[1]) : originals[1];
        glass.glassSize = originalSize * (skin != null ? skin.glassSizeScale : 1f);
        glass.UpdateGlass();
    }

    private static Quaternion SkinRotation(GameObject tile)
    {
        ConveyorTile conveyor = tile.GetComponent<ConveyorTile>();
        if (conveyor != null && conveyor.direction != Vector3.zero)
        {
            return Quaternion.LookRotation(conveyor.direction, Vector3.up);
        }

        if (tile.GetComponent<BridgeController>() != null && BridgeRunsAlongX(tile.transform.position))
        {
            return Quaternion.Euler(0f, 90f, 0f);
        }

        return Quaternion.identity;
    }

    /// <summary>Köprü mesh'inin tahtaları X boyunca uzanır (yol Z yönünde); sağ/sol komşusu fazlaysa 90° döndürülür.</summary>
    private static bool BridgeRunsAlongX(Vector3 position)
    {
        LevelData data = LevelLoader.Instance != null ? LevelLoader.Instance.CurrentLevelData : null;
        if (data == null || data.layout == null)
        {
            return false;
        }

        int c = Mathf.RoundToInt(position.x);
        int r = -Mathf.RoundToInt(position.z);
        int alongX = (HasCell(data.layout, c - 1, r) ? 1 : 0) + (HasCell(data.layout, c + 1, r) ? 1 : 0);
        int alongZ = (HasCell(data.layout, c, r - 1) ? 1 : 0) + (HasCell(data.layout, c, r + 1) ? 1 : 0);
        return alongX > alongZ;
    }

    private static bool HasCell(string[] layout, int c, int r)
    {
        if (r < 0 || r >= layout.Length || c < 0)
        {
            return false;
        }

        string[] tokens = layout[r].Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
        return c < tokens.Length && tokens[c].Trim() != CellMapType.NOTHING;
    }

    private static void ShowSkinMesh(GameObject tile, ref Transform skinMesh, TileSkin.Rule shape)
    {
        if (skinMesh == null)
        {
            Bounds bounds = default(Bounds);
            bool hasBounds = false;
            foreach (Renderer r in tile.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is SpriteRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            Vector3 tilePos = tile.transform.position;
            Vector3 topCenter = hasBounds
                ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)
                : new Vector3(tilePos.x, 0f, tilePos.z);

            var go = new GameObject(SkinMeshName, typeof(MeshFilter), typeof(MeshRenderer));
            skinMesh = go.transform;
            skinMesh.SetParent(tile.transform, false);
            skinMesh.position = topCenter;
            skinMesh.rotation = SkinRotation(tile);
            float parentScale = Mathf.Max(0.0001f, tile.transform.lossyScale.x);
            skinMesh.localScale = Vector3.one / parentScale;
        }

        skinMesh.gameObject.SetActive(true);
        skinMesh.GetComponent<MeshFilter>().sharedMesh = shape.mesh;

        Vector3 p = tile.transform.position;
        bool alternate = ((Mathf.RoundToInt(p.x) + Mathf.RoundToInt(p.z)) & 1) == 1;
        bool useChecker = alternate && shape.checkerMaterials != null && shape.checkerMaterials.Length > 0;
        skinMesh.GetComponent<MeshRenderer>().sharedMaterials = useChecker ? shape.checkerMaterials : shape.meshMaterials;
    }

    public void ApplyToAllInScene()
    {
        if (LevelLoader.Instance != null)
        {
            foreach (GameObject tile in LevelLoader.Instance.SpawnedTiles)
            {
                ApplyTileSkin(tile);
            }
        }

        TumbleController[] cubes = FindObjectsOfType<TumbleController>(true);
        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] != null)
            {
                ApplyTo(cubes[i].gameObject);
                if (cubes[i].player1 != null)
                {
                    ApplyTo(cubes[i].player1);
                }

                if (cubes[i].player2 != null)
                {
                    ApplyTo(cubes[i].player2);
                }
            }
        }

        // Tag ile yakalanan player (TumbleController henüz yoksa)
        GameObject[] tagged = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < tagged.Length; i++)
        {
            ApplyTo(tagged[i]);
        }
    }
}
