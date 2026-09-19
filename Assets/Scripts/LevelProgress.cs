using UnityEngine;

/// <summary>
/// Sıralı seviye kilidi. PlayerPrefs: MaxUnlockedAbsolute (1 = yalnızca w1l1 açık).
/// Seviye bitince bir sonraki açılır. SavedWorld/SavedLevel Continue için ayrı tutulur.
/// </summary>
public static class LevelProgress
{
    public const string PrefMaxUnlockedAbsolute = "MaxUnlockedAbsolute";
    public const int LevelsPerWorld = 20;
    public const int MaxWorld = 15;

    public static void EnsureDefaults()
    {
        if (PlayerPrefs.HasKey(PrefMaxUnlockedAbsolute))
            return;

        int max = 1;
        int savedWorld = PlayerPrefs.GetInt("SavedWorld", 1);
        int savedLevel = PlayerPrefs.GetInt("SavedLevel", 1);
        max = Mathf.Max(max, GetAbsoluteIndex(savedWorld, savedLevel));

        for (int w = 1; w <= MaxWorld; w++)
        {
            for (int l = 1; l <= LevelsPerWorld; l++)
            {
                if (!LevelStarRating.HasPlayedLevel(w, l))
                    continue;
                max = Mathf.Max(max, GetAbsoluteIndex(w, l) + 1);
            }
        }

        max = Mathf.Clamp(max, 1, MaxWorld * LevelsPerWorld);
        PlayerPrefs.SetInt(PrefMaxUnlockedAbsolute, max);
        PlayerPrefs.Save();
        Debug.Log($"[LevelProgress] İlk kurulum / migrasyon → unlocked absolute {max}");
    }

    public static int GetMaxUnlockedAbsolute()
    {
        EnsureDefaults();
        return Mathf.Max(1, PlayerPrefs.GetInt(PrefMaxUnlockedAbsolute, 1));
    }

    public static int GetAbsoluteIndex(int world, int level)
    {
        int w = Mathf.Max(1, world);
        int l = Mathf.Clamp(level, 1, LevelsPerWorld);
        return (w - 1) * LevelsPerWorld + l;
    }

    public static void AbsoluteToWorldLevel(int absolute, out int world, out int level)
    {
        absolute = Mathf.Max(1, absolute);
        world = (absolute - 1) / LevelsPerWorld + 1;
        level = (absolute - 1) % LevelsPerWorld + 1;
    }

    public static bool IsLevelUnlocked(int world, int level)
    {
        return GetAbsoluteIndex(world, level) <= GetMaxUnlockedAbsolute();
    }

    public static bool IsWorldUnlocked(int world)
    {
        return IsLevelUnlocked(world, 1);
    }

    /// <summary>
    /// Seviye tamamlanınca, kazanılan yıldız MinStarsToAdvance eşiğini karşılıyorsa bir sonrakini açar.
    /// Eşik tutmazsa mevcut kilit durumu değişmez (geriye dönük kilitleme yok).
    /// </summary>
    /// <returns>Sonraki seviye bu çağrıyla veya önceden açıksa true.</returns>
    public static bool NotifyLevelCompleted(int world, int level, int earnedStars)
    {
        EnsureDefaults();
        int completedAbs = GetAbsoluteIndex(world, level);
        int nextAbs = completedAbs + 1;
        int maxTotal = MaxWorld * LevelsPerWorld;
        if (nextAbs > maxTotal)
            nextAbs = maxTotal;

        int required = GameplaySettings.MinStarsToAdvance;
        if (earnedStars < required)
        {
            Debug.Log($"[LevelProgress] No unlock — stars {earnedStars} < required {required} (w{world}l{level})");
            return IsLevelUnlockedAbsolute(nextAbs);
        }

        int current = GetMaxUnlockedAbsolute();
        // En az tamamlanan seviye açık kalsın; bir sonrakini de aç
        int desired = Mathf.Max(current, completedAbs, nextAbs);
        if (desired > current)
        {
            PlayerPrefs.SetInt(PrefMaxUnlockedAbsolute, desired);
            PlayerPrefs.Save();
            Debug.Log($"[LevelProgress] Unlock → absolute {desired} (completed w{world}l{level}, stars {earnedStars})");
        }

        return IsLevelUnlockedAbsolute(nextAbs);
    }

    private static bool IsLevelUnlockedAbsolute(int absolute)
    {
        return absolute <= GetMaxUnlockedAbsolute();
    }
}
