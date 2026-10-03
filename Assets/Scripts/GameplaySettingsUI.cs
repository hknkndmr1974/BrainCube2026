using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainMenu Gameplay: Complete ★, Move Speed, Hint Button ON/OFF.
/// </summary>
public sealed class GameplaySettingsUI : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private Slider difficultySlider;
    private Text difficultyLabel;
    private Slider moveSpeedSlider;
    private Text moveSpeedLabel;
    private Toggle hintButtonToggle;
    private Text hintButtonLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(scene.name) || !scene.name.StartsWith("MainMenu"))
        {
            return;
        }

        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate.name != "GamePlayWindow" ||
                candidate.gameObject.scene != scene)
            {
                continue;
            }

            if (candidate.GetComponent<GameplaySettingsUI>() == null)
            {
                candidate.gameObject.AddComponent<GameplaySettingsUI>();
            }

            return;
        }

        Debug.LogWarning("GameplaySettingsUI: MainMenu/GamePlayWindow bulunamadı.");
    }

    private void Awake()
    {
        difficultySlider = FindChildComponent<Slider>("Dificulty Slider");
        difficultyLabel = FindChildComponent<Text>("Dificulty");
        moveSpeedSlider = FindChildComponent<Slider>("Movement Speed");
        moveSpeedLabel = FindChildComponent<Text>("Movement Speed");
        hintButtonToggle = FindChildComponent<Toggle>("AO Toggle");
        hintButtonLabel = FindChildComponent<Text>("Perma Death");

        ConfigureDifficultySlider();
        ConfigureMoveSpeedSlider();
        ConfigureHintButtonToggle();
        RefreshFromSettings();
    }

    private void OnEnable()
    {
        GameplaySettings.SettingsChanged += RefreshFromSettings;
        RefreshFromSettings();
    }

    private void OnDisable()
    {
        GameplaySettings.SettingsChanged -= RefreshFromSettings;
    }

    private void ConfigureDifficultySlider()
    {
        if (difficultySlider == null)
        {
            return;
        }

        difficultySlider.minValue = GameplaySettings.MinStars;
        difficultySlider.maxValue = GameplaySettings.MaxStars;
        difficultySlider.wholeNumbers = true;
        difficultySlider.onValueChanged = new Slider.SliderEvent();
        difficultySlider.onValueChanged.AddListener(OnDifficultyChanged);
    }

    private void ConfigureMoveSpeedSlider()
    {
        if (moveSpeedSlider == null)
        {
            return;
        }

        moveSpeedSlider.minValue = GameplaySettings.MinMoveSpeedMultiplier;
        moveSpeedSlider.maxValue = GameplaySettings.MaxMoveSpeedMultiplier;
        moveSpeedSlider.wholeNumbers = false;
        moveSpeedSlider.onValueChanged = new Slider.SliderEvent();
        moveSpeedSlider.onValueChanged.AddListener(OnMoveSpeedChanged);
    }

    private void ConfigureHintButtonToggle()
    {
        if (hintButtonToggle == null)
        {
            return;
        }

        hintButtonToggle.onValueChanged = new Toggle.ToggleEvent();
        hintButtonToggle.onValueChanged.AddListener(OnHintButtonToggled);
    }

    private void RefreshFromSettings()
    {
        int stars = GameplaySettings.MinStarsToAdvance;
        if (difficultySlider != null)
        {
            difficultySlider.SetValueWithoutNotify(stars);
        }

        RefreshDifficultyLabel(stars);

        float speed = GameplaySettings.MoveSpeedMultiplier;
        if (moveSpeedSlider != null)
        {
            moveSpeedSlider.SetValueWithoutNotify(speed);
        }

        RefreshMoveSpeedLabel(speed);

        bool showHint = GameplaySettings.ShowHintButton;
        if (hintButtonToggle != null)
        {
            hintButtonToggle.SetIsOnWithoutNotify(showHint);
        }

        RefreshHintButtonLabel(showHint);
    }

    private void OnDifficultyChanged(float value)
    {
        int stars = Mathf.RoundToInt(value);
        GameplaySettings.SetMinStarsToAdvance(stars);
        RefreshDifficultyLabel(stars);
    }

    private void OnMoveSpeedChanged(float value)
    {
        GameplaySettings.SetMoveSpeedMultiplier(value);
        RefreshMoveSpeedLabel(value);
    }

    private void OnHintButtonToggled(bool isOn)
    {
        GameplaySettings.SetShowHintButton(isOn);
        RefreshHintButtonLabel(isOn);
    }

    private void RefreshDifficultyLabel(int stars)
    {
        if (difficultyLabel != null)
        {
            difficultyLabel.text = $"Level Complete: {stars}★";
        }
    }

    private void RefreshMoveSpeedLabel(float multiplier)
    {
        if (moveSpeedLabel != null)
        {
            int percent = Mathf.RoundToInt(multiplier * 100f);
            moveSpeedLabel.text = $"Move Speed: {percent}%";
        }
    }

    private void RefreshHintButtonLabel(bool isOn)
    {
        if (hintButtonLabel != null)
        {
            hintButtonLabel.text = isOn ? "Hint Button: ON" : "Hint Button: OFF";
        }
    }

    private T FindChildComponent<T>(string objectName) where T : Component
    {
        foreach (T component in GetComponentsInChildren<T>(true))
        {
            if (component.gameObject.name == objectName)
            {
                return component;
            }
        }

        Debug.LogWarning($"GameplaySettingsUI: '{objectName}' ({typeof(T).Name}) bulunamadı.", this);
        return null;
    }
}
