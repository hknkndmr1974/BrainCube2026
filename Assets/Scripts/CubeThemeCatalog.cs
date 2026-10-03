using System;
using UnityEngine;

[Serializable]
public class CubeThemeEntry
{
    public string displayName = "Theme";
    [Tooltip("Wood / Metal / Wall gibi sekme adı. Boşsa 'General'.")]
    public string category = "General";
    [Tooltip("Boş bırakılırsa küpün orijinal (Default) material'i kullanılır.")]
    public Material material;
    [Tooltip("Boş bırakılırsa küpün orijinal mesh'i kullanılır (ör. yuvarlak köşeli küp için RoundedCube).")]
    public Mesh mesh;
    [Tooltip("Boş bırakılırsa zemin karoları orijinal malzemeleriyle kalır.")]
    public TileSkin tileSkin;
}

/// <summary>
/// Inspector'dan material listesi atanır. Resources/CubeThemeCatalog asset'ini düzenle.
/// </summary>
[CreateAssetMenu(fileName = "CubeThemeCatalog", menuName = "BrainCube/Cube Theme Catalog", order = 10)]
public class CubeThemeCatalog : ScriptableObject
{
    [Tooltip("Her satır bir tema. Category alanı sekmeleri oluşturur (Wood, Metal, Wall...).")]
    public CubeThemeEntry[] themes = new CubeThemeEntry[]
    {
        new CubeThemeEntry { displayName = "Default", category = "Wood", material = null }
    };

    [Tooltip("Oyuncu henüz tema seçmediyse kullanılan tema (themes dizisi indeksi).")]
    public int defaultThemeIndex;

    public int Count => themes != null ? themes.Length : 0;

    public CubeThemeEntry Get(int index)
    {
        if (themes == null || themes.Length == 0)
        {
            return new CubeThemeEntry { displayName = "Default", category = "Wood", material = null };
        }

        index = Mathf.Clamp(index, 0, themes.Length - 1);
        return themes[index] ?? new CubeThemeEntry { displayName = "Default", category = "Wood", material = null };
    }
}
