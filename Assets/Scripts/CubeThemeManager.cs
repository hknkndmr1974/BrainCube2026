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
        get => Mathf.Clamp(PlayerPrefs.GetInt(PrefSelectedThemeIndex, 0), 0, Mathf.Max(0, ThemeCount - 1));
        private set
        {
            PlayerPrefs.SetInt(PrefSelectedThemeIndex, value);
            PlayerPrefs.Save();
        }
    }

    public int ThemeCount => Catalog != null ? Catalog.Count : 0;

    private readonly Dictionary<int, Material[]> originalMaterials = new Dictionary<int, Material[]>();

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

        Material selected = GetSelectedMaterial();

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

    public void ApplyToAllInScene()
    {
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
