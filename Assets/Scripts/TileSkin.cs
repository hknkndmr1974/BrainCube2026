using System;
using UnityEngine;

/// <summary>
/// Zemin karolarının malzemelerini temaya göre değiştirir. CubeThemeEntry.tileSkin ile bağlanır;
/// boşsa karolar prefabdaki orijinal malzemeleriyle kalır.
/// </summary>
[CreateAssetMenu(fileName = "TileSkin", menuName = "BrainCube/Tile Skin", order = 11)]
public class TileSkin : ScriptableObject
{
    [Serializable]
    public class MaterialSwap
    {
        public Material from;
        public Material to;
    }

    [Serializable]
    public class Rule
    {
        [Tooltip("Karo adının başlangıcı (LevelLoader'ın verdiği ad: NormalTile, GoalTile, BridgeTile_...). Boş = tüm karolar.")]
        public string namePrefix = "";
        public MaterialSwap[] swaps = new MaterialSwap[0];

        [Tooltip("Doluysa karonun orijinal görseli gizlenir, yerine bu mesh gösterilir (üst yüzü y = 0). Çarpışma kutuları değişmez.")]
        public Mesh mesh;
        [Tooltip("Mesh'in submesh sırasına göre malzemeler.")]
        public Material[] meshMaterials = new Material[0];
        [Tooltip("Doluysa dama deseninde her ikinci karoda meshMaterials yerine bunlar kullanılır.")]
        public Material[] checkerMaterials = new Material[0];
    }

    [Tooltip("Yukarıdan aşağı aranır; karo adına uyan ve 'from' malzemesi eşleşen ilk kural kullanılır.")]
    public Rule[] rules = new Rule[0];

    [Tooltip("Cam (WeakTile) panelinin genişlik çarpanı; 1 = prefab boyutu.")]
    [Range(0.5f, 1f)]
    public float glassSizeScale = 1f;

    public Rule FindShapeRule(string tileName)
    {
        if (rules == null)
        {
            return null;
        }

        for (int r = 0; r < rules.Length; r++)
        {
            Rule rule = rules[r];
            if (rule != null && rule.mesh != null && Matches(rule, tileName))
            {
                return rule;
            }
        }

        return null;
    }

    private static bool Matches(Rule rule, string tileName)
    {
        return string.IsNullOrEmpty(rule.namePrefix) ||
               (tileName != null && tileName.StartsWith(rule.namePrefix, StringComparison.Ordinal));
    }

    public Material Resolve(string tileName, Material original)
    {
        if (original == null || rules == null)
        {
            return original;
        }

        for (int r = 0; r < rules.Length; r++)
        {
            Rule rule = rules[r];
            if (rule == null || rule.swaps == null)
            {
                continue;
            }

            if (!Matches(rule, tileName))
            {
                continue;
            }

            for (int s = 0; s < rule.swaps.Length; s++)
            {
                MaterialSwap swap = rule.swaps[s];
                if (swap != null && swap.from == original && swap.to != null)
                {
                    return swap.to;
                }
            }
        }

        return original;
    }
}
