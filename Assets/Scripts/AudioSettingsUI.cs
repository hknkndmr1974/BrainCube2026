using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Binds the existing Settings/Audio sliders to AudioManager without
/// requiring serialized scene references.
/// </summary>
public sealed class AudioSettingsUI : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private Slider masterSlider;
    private Slider musicSlider;
    private Slider effectsSlider;

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
            if (candidate.name != "AudioWindow" ||
                candidate.gameObject.scene != scene)
            {
                continue;
            }

            if (candidate.GetComponent<AudioSettingsUI>() == null)
            {
                candidate.gameObject.AddComponent<AudioSettingsUI>();
            }

            return;
        }

        Debug.LogWarning("AudioSettingsUI: MainMenu/SettingsPanels/AudioWindow bulunamadı.");
    }

    private void Awake()
    {
        masterSlider = FindSlider("Master Volume Slider");
        musicSlider = FindSlider("Music slider");
        effectsSlider = FindSlider("SFX Slider");

        ConfigureSlider(masterSlider, OnMasterVolumeChanged);
        ConfigureSlider(musicSlider, OnMusicVolumeChanged);
        ConfigureSlider(effectsSlider, OnEffectsVolumeChanged);

        Text effectsLabel = FindChildComponent<Text>("Sfx");
        if (effectsLabel != null)
        {
            effectsLabel.text = "SFX / UI";
        }
    }

    private void OnEnable()
    {
        AudioManager.SettingsChanged += RefreshValues;
        RefreshValues();
    }

    private void OnDisable()
    {
        AudioManager.SettingsChanged -= RefreshValues;
    }

    private void RefreshValues()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return;
        }

        SetValueWithoutNotify(masterSlider, audio.MasterVolume);
        SetValueWithoutNotify(musicSlider, audio.MusicVolume);
        SetValueWithoutNotify(effectsSlider, audio.SfxVolume);
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
    }

    private void OnEffectsVolumeChanged(float value)
    {
        AudioManager.Instance?.SetEffectsVolume(value);
    }

    private Slider FindSlider(string objectName)
    {
        return FindChildComponent<Slider>(objectName);
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

        Debug.LogWarning($"AudioSettingsUI: '{objectName}' kontrolü bulunamadı.", this);
        return null;
    }

    private static void ConfigureSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged = new Slider.SliderEvent();
        slider.onValueChanged.AddListener(callback);
    }

    private static void SetValueWithoutNotify(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
    }
}
