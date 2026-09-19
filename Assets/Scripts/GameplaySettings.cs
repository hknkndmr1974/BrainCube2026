using System;
using UnityEngine;

/// <summary>
/// Gameplay ayarları: level clear yıldız eşiği, küp hızı (±%20), hint butonu görünürlüğü.
/// </summary>
public static class GameplaySettings
{
    public const string PrefMinStarsToAdvance = "MinStarsToAdvance";
    public const string PrefMoveSpeedMultiplier = "MoveSpeedMultiplier";
    public const string PrefShowHintButton = "ShowHintButton";

    public const int DefaultMinStarsToAdvance = 1;
    public const int MinStars = 1;
    public const int MaxStars = 3;

    public const float DefaultMoveSpeedMultiplier = 1f;
    public const float MinMoveSpeedMultiplier = 0.8f;
    public const float MaxMoveSpeedMultiplier = 1.2f;

    public const int DefaultShowHintButton = 1;

    public static event Action SettingsChanged;

    public static int MinStarsToAdvance =>
        Mathf.Clamp(PlayerPrefs.GetInt(PrefMinStarsToAdvance, DefaultMinStarsToAdvance), MinStars, MaxStars);

    public static float MoveSpeedMultiplier =>
        Mathf.Clamp(PlayerPrefs.GetFloat(PrefMoveSpeedMultiplier, DefaultMoveSpeedMultiplier),
            MinMoveSpeedMultiplier, MaxMoveSpeedMultiplier);

    public static bool ShowHintButton =>
        PlayerPrefs.GetInt(PrefShowHintButton, DefaultShowHintButton) != 0;

    public static void SetMinStarsToAdvance(int value)
    {
        value = Mathf.Clamp(value, MinStars, MaxStars);
        if (PlayerPrefs.GetInt(PrefMinStarsToAdvance, DefaultMinStarsToAdvance) == value &&
            PlayerPrefs.HasKey(PrefMinStarsToAdvance))
        {
            return;
        }

        PlayerPrefs.SetInt(PrefMinStarsToAdvance, value);
        PlayerPrefs.Save();
        SettingsChanged?.Invoke();
    }

    public static void SetMoveSpeedMultiplier(float value)
    {
        value = Mathf.Clamp(value, MinMoveSpeedMultiplier, MaxMoveSpeedMultiplier);
        if (PlayerPrefs.HasKey(PrefMoveSpeedMultiplier) &&
            Mathf.Approximately(PlayerPrefs.GetFloat(PrefMoveSpeedMultiplier), value))
        {
            return;
        }

        PlayerPrefs.SetFloat(PrefMoveSpeedMultiplier, value);
        PlayerPrefs.Save();
        SettingsChanged?.Invoke();
    }

    public static void SetShowHintButton(bool visible)
    {
        int stored = visible ? 1 : 0;
        if (PlayerPrefs.HasKey(PrefShowHintButton) &&
            PlayerPrefs.GetInt(PrefShowHintButton) == stored)
        {
            return;
        }

        PlayerPrefs.SetInt(PrefShowHintButton, stored);
        PlayerPrefs.Save();
        SettingsChanged?.Invoke();
    }
}
