using System;
using UnityEngine;

/// <summary>
/// Level yıldız değerlendirmesi: playerMoves / minMoves oranına göre 0-3 yıldız.
/// En iyi skor PlayerPrefs'te level bazında saklanır.
/// </summary>
public static class LevelStarRating
{
    public const float ThreeStarMaxRatio = 1.15f;
    public const float TwoStarMaxRatio = 1.35f;
    public const float OneStarMaxRatio = 1.75f;

    private const string PrefKeyFormat = "Stars_w{0}_l{1}";

    public static int GetMinimumMoves(LevelData data)
    {
        if (data == null)
        {
            return 0;
        }

        if (data.minMoves > 0)
        {
            return data.minMoves;
        }

        if (string.IsNullOrWhiteSpace(data.hintMoves))
        {
            return 0;
        }

        return data.hintMoves.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public static int Evaluate(int playerMoves, int minMoves)
    {
        if (playerMoves < 0)
        {
            playerMoves = 0;
        }

        if (minMoves <= 0)
        {
            // Hedef bilinmiyorsa level bitirme ödülü olarak 1 yıldız
            return 1;
        }

        float ratio = (float)playerMoves / minMoves;

        if (ratio <= ThreeStarMaxRatio) return 3;
        if (ratio <= TwoStarMaxRatio) return 2;
        if (ratio <= OneStarMaxRatio) return 1;
        return 0;
    }

    public static string GetPrefsKey(int world, int level)
    {
        return string.Format(PrefKeyFormat, world, level);
    }

    public static int GetBestStars(int world, int level)
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(GetPrefsKey(world, level), 0), 0, 3);
    }

    public static bool HasPlayedLevel(int world, int level)
    {
        return PlayerPrefs.HasKey(GetPrefsKey(world, level));
    }

    /// <summary>
    /// Yeni skoru kaydeder; yalnızca önceki best'ten yüksekse günceller.
    /// İlk tamamlamada (0 yıldız dahil) anahtar yazılır ki level "oynandı" sayılır.
    /// Dönen değer kayıttaki (best) yıldız sayısıdır.
    /// </summary>
    public static int SaveBestStars(int world, int level, int earnedStars)
    {
        earnedStars = Mathf.Clamp(earnedStars, 0, 3);
        string key = GetPrefsKey(world, level);

        if (!PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.SetInt(key, earnedStars);
            PlayerPrefs.Save();
            return earnedStars;
        }

        int best = GetBestStars(world, level);
        if (earnedStars > best)
        {
            PlayerPrefs.SetInt(key, earnedStars);
            PlayerPrefs.Save();
            return earnedStars;
        }

        return best;
    }
}
