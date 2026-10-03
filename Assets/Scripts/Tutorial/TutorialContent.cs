using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Kartların gösterim sırası enum sırasıdır.</summary>
public enum TutorialMechanic
{
    Goal,
    SoftSwitch,
    Bridge,
    HardSwitch,
    Fragile,
    OneWaySwitch,
    Teleport,
    Conveyor,
    Split
}

public enum TutorialTip
{
    GoalLying,
    FragileBroke,
    HardSwitchLying,
    StuckUseHint,
    Swipe
}

public static class TutorialContent
{
    public struct Card
    {
        public string Title;
        public string Body;
        public Color Accent;
    }

    public static bool IsTurkish => GameText.IsTurkish;

    public static string TapToContinue => IsTurkish ? "Devam etmek için dokun" : "Tap to continue";

    public static Card GetCard(TutorialMechanic mechanic)
    {
        bool tr = IsTurkish;
        switch (mechanic)
        {
            case TutorialMechanic.Goal:
                return Make(
                    tr ? "Hedefe Ulaş" : "Reach the Goal",
                    tr ? "Küpü yuvarlamak için ekranı kaydır. Bölümü bitirmek için küpü hedef deliğe dik olarak sok."
                       : "Swipe to roll the block. Drop it into the goal hole standing upright to finish the level.",
                    new Color(0.25f, 0.78f, 0.55f));
            case TutorialMechanic.SoftSwitch:
                return Make(
                    tr ? "Yumuşak Switch" : "Soft Switch",
                    tr ? "Bu switch'e küpün herhangi bir yüzüyle basman yeterli. Bağlı köprüleri açar ya da kapatır."
                       : "Press this switch with any side of the block. It opens or closes the linked bridges.",
                    new Color(0.98f, 0.80f, 0.25f));
            case TutorialMechanic.Bridge:
                return Make(
                    tr ? "Köprüler" : "Bridges",
                    tr ? "Köprüler switch'lerle açılıp kapanır. Kapalı bir köprünün üstüne gidersen düşersin."
                       : "Bridges are opened and closed by switches. Step onto a closed bridge and you will fall.",
                    new Color(0.30f, 0.80f, 0.95f));
            case TutorialMechanic.HardSwitch:
                return Make(
                    tr ? "Sert Switch" : "Heavy Switch",
                    tr ? "Bu switch yalnızca küp üzerinde dik dururken çalışır. Yatarak basmak işe yaramaz."
                       : "This switch only works when the block stands upright on it. Lying flat won't press it.",
                    new Color(0.95f, 0.38f, 0.38f));
            case TutorialMechanic.Fragile:
                return Make(
                    tr ? "Kırılgan Karo" : "Fragile Tile",
                    tr ? "Bu karolar dik duran küpün ağırlığını taşıyamaz ve kırılır. Üzerinden yatarak geç."
                       : "These tiles break under an upright block. Roll across them lying flat.",
                    new Color(0.95f, 0.60f, 0.30f));
            case TutorialMechanic.OneWaySwitch:
                return Make(
                    tr ? "Tek Yönlü Switch" : "One-Way Switch",
                    tr ? "Bazı switch'ler köprüyü yalnızca açar ya da yalnızca kapatır. Basma sırasını iyi planla."
                       : "Some switches only open or only close a bridge. Plan the order you press them.",
                    new Color(0.75f, 0.55f, 0.95f));
            case TutorialMechanic.Teleport:
                return Make(
                    tr ? "Teleport" : "Teleporter",
                    tr ? "Küpü teleport karosunun üstüne dik getir; seni bölümün başka bir noktasına ışınlar."
                       : "Stand the block upright on a teleporter to warp to another part of the level.",
                    new Color(0.90f, 0.40f, 0.90f));
            case TutorialMechanic.Conveyor:
                return Make(
                    tr ? "Taşıma Bandı" : "Conveyor",
                    tr ? "Küp bandın üstünde dik durduğunda bant onu okun gösterdiği yöne taşır."
                       : "When the block stands upright on a conveyor, it gets carried in the arrow's direction.",
                    new Color(1.00f, 0.60f, 0.15f));
            case TutorialMechanic.Split:
                return Make(
                    tr ? "Bölünme" : "Split",
                    tr ? "Bu karoya dik basınca küp ikiye ayrılır. Alttaki butonla küpler arasında geçiş yap; yan yana getirince birleşirler."
                       : "Stand on this tile to split the block in two. Use the button below to switch between them; bring them side by side to merge.",
                    new Color(0.35f, 0.85f, 0.85f));
            default:
                return Make(mechanic.ToString(), string.Empty, Color.white);
        }
    }

    public static string GetTip(TutorialTip tip)
    {
        bool tr = IsTurkish;
        switch (tip)
        {
            case TutorialTip.GoalLying:
                return tr ? "Hedefe dik girmelisin!" : "Enter the goal standing upright!";
            case TutorialTip.FragileBroke:
                return tr ? "Kırılgan karolardan yatarak geç." : "Cross fragile tiles lying flat.";
            case TutorialTip.HardSwitchLying:
                return tr ? "Bu switch yalnızca dik basınca çalışır." : "This switch only works when standing upright.";
            case TutorialTip.StuckUseHint:
                return tr ? "Takıldın mı? İpucu butonunu deneyebilirsin." : "Stuck? Try the hint button.";
            case TutorialTip.Swipe:
                return tr ? "Kaydır" : "Swipe";
            default:
                return string.Empty;
        }
    }

    /// <summary>Bölüm düzenindeki mekanikleri ve her birinin ilk görüldüğü hücreyi (sütun, satır) döner.</summary>
    public static SortedDictionary<TutorialMechanic, Vector2Int> DetectMechanics(LevelData data)
    {
        var result = new SortedDictionary<TutorialMechanic, Vector2Int>();
        if (data == null || data.layout == null) return result;

        for (int r = 0; r < data.layout.Length; r++)
        {
            string[] tokens = data.layout[r].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int c = 0; c < tokens.Length; c++)
            {
                string token = tokens[c].Trim();
                if (token == CellMapType.NOTHING) continue;

                var cell = new Vector2Int(c, r);
                foreach (string sub in token.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (sub == CellMapType.HOME)
                    {
                        Add(result, TutorialMechanic.Goal, cell);
                        continue;
                    }

                    string type = LetterPrefix(sub);
                    switch (type)
                    {
                        case "s":
                            Add(result, TutorialMechanic.SoftSwitch, cell);
                            break;
                        case "so":
                        case "sc":
                            Add(result, TutorialMechanic.SoftSwitch, cell);
                            Add(result, TutorialMechanic.OneWaySwitch, cell);
                            break;
                        case "h":
                            Add(result, TutorialMechanic.HardSwitch, cell);
                            break;
                        case "ho":
                        case "hc":
                            Add(result, TutorialMechanic.HardSwitch, cell);
                            Add(result, TutorialMechanic.OneWaySwitch, cell);
                            break;
                        case "b":
                        case "B":
                            Add(result, TutorialMechanic.Bridge, cell);
                            break;
                        case "w":
                            Add(result, TutorialMechanic.Fragile, cell);
                            break;
                        case "t":
                            Add(result, TutorialMechanic.Teleport, cell);
                            break;
                        case "m":
                            Add(result, TutorialMechanic.Conveyor, cell);
                            break;
                        case "ms":
                            Add(result, TutorialMechanic.Split, cell);
                            break;
                    }
                }
            }
        }

        return result;
    }

    private static void Add(SortedDictionary<TutorialMechanic, Vector2Int> map, TutorialMechanic mechanic, Vector2Int cell)
    {
        if (!map.ContainsKey(mechanic)) map[mechanic] = cell;
    }

    private static string LetterPrefix(string token)
    {
        int i = 0;
        while (i < token.Length && char.IsLetter(token[i])) i++;
        return token.Substring(0, i);
    }

    private static Card Make(string title, string body, Color accent)
    {
        return new Card { Title = title, Body = body, Accent = accent };
    }
}
