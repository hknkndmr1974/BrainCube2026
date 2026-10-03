using UnityEngine;

public static class GameText
{
    // Menüler sahnede sabit İngilizce; tam çeviri yapılana kadar Türkçe kapalı kalmalı.
    private static readonly bool TurkishEnabled = false;

    public static bool IsTurkish => TurkishEnabled && Application.systemLanguage == SystemLanguage.Turkish;

    public static string Pick(string turkish, string english) => IsTurkish ? turkish : english;
}
