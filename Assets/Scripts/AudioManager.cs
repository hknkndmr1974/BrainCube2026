using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent audio service with separate Music, SFX and UI channels.
/// It is created automatically before the first scene and stores settings
/// in PlayerPrefs so the Settings/Audio screen can bind to it later.
/// </summary>
public sealed class AudioManager : MonoBehaviour
{
    private const string MasterVolumeKey = "Audio.MasterVolume";
    private const string MusicVolumeKey = "Audio.MusicVolume";
    private const string SfxVolumeKey = "Audio.SfxVolume";
    private const string UiVolumeKey = "Audio.UiVolume";
    private const string MutedKey = "Audio.Muted";

    private const int SfxPoolSize = 12;
    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 0.65f;
    private const float DefaultSfxVolume = 0.85f;
    private const float DefaultUiVolume = 0.8f;

    public static AudioManager Instance { get; private set; }

    public static event Action SettingsChanged;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }
    public float UiVolume { get; private set; }
    public bool IsMuted { get; private set; }
    public AudioEventLibrary EventLibrary { get; private set; }

    private AudioSource musicSource;
    private AudioSource uiSource;
    private AudioSource[] sfxSources;
    private int nextSfxSource;
    private float musicVolumeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject audioManager = new GameObject(nameof(AudioManager));
        audioManager.AddComponent<AudioManager>();
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

        EventLibrary = Resources.Load<AudioEventLibrary>("AudioEventLibrary");
        LoadSettings();
        CreateAudioSources();
        ApplySettings();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySceneMusic(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    public bool PlayEvent(
        AudioEventId eventId,
        Vector3 position,
        AudioClip fallbackClip = null,
        float fallbackVolume = 1f,
        float fallbackPitchVariation = 0f)
    {
        AudioCue cue = EventLibrary != null ? EventLibrary.GetCue(eventId) : null;
        AudioClip clip = cue?.clip != null ? cue.clip : fallbackClip;
        if (clip == null)
        {
            return false;
        }

        float volume = cue?.clip != null ? cue.volume : fallbackVolume;
        float pitchVariation = cue?.clip != null ? cue.pitchVariation : fallbackPitchVariation;
        float pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        PlaySfx(clip, position, volume, pitch);
        return true;
    }

    public bool PlayUiEvent(AudioEventId eventId)
    {
        AudioCue cue = EventLibrary != null ? EventLibrary.GetCue(eventId) : null;
        if (cue?.clip == null)
        {
            return false;
        }

        float pitch = 1f + UnityEngine.Random.Range(-cue.pitchVariation, cue.pitchVariation);
        PlayUi(cue.clip, cue.volume, pitch);
        return true;
    }

    public void PlayMusic(AudioClip clip, bool restartIfSame = false, float volumeScale = 1f)
    {
        if (clip == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying && !restartIfSame)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        musicVolumeScale = Mathf.Clamp01(volumeScale);
        musicSource.volume = musicVolumeScale * MusicVolume;
        musicSource.Play();
    }

    public void StopMusic(float fadeDuration = 0f)
    {
        // Fade support can be added when scene-specific music is introduced.
        musicSource.Stop();
        musicSource.clip = null;
    }

    public void PauseMusic(bool paused)
    {
        if (paused)
        {
            musicSource.Pause();
        }
        else
        {
            musicSource.UnPause();
        }
    }

    public void PlayUi(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || IsMuted)
        {
            return;
        }

        uiSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        uiSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlaySfx(AudioClip clip, Vector3 position, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || IsMuted)
        {
            return;
        }

        AudioSource source = GetSfxSource();
        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volumeScale) * SfxVolume;
        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        source.Play();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    public void SetEffectsVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        SfxVolume = volume;
        UiVolume = volume;
        SaveAndApplySettings();
    }

    public void SetUiVolume(float value)
    {
        UiVolume = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        SaveAndApplySettings();
    }

    public void ToggleMuted()
    {
        SetMuted(!IsMuted);
    }

    public void ResetSettings()
    {
        MasterVolume = DefaultMasterVolume;
        MusicVolume = DefaultMusicVolume;
        SfxVolume = DefaultSfxVolume;
        UiVolume = DefaultUiVolume;
        IsMuted = false;
        SaveAndApplySettings();
    }

    private void CreateAudioSources()
    {
        musicSource = CreateSource("Music", 0f);
        musicSource.loop = true;

        uiSource = CreateSource("UI", 0f);

        sfxSources = new AudioSource[SfxPoolSize];
        for (int i = 0; i < sfxSources.Length; i++)
        {
            AudioSource source = CreateSource($"SFX_{i + 1:00}", 1f);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.dopplerLevel = 0f;
            sfxSources[i] = source;
        }
    }

    private AudioSource CreateSource(string sourceName, float spatialBlend)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.bypassEffects = false;
        source.bypassListenerEffects = false;
        source.bypassReverbZones = false;
        return source;
    }

    private AudioSource GetSfxSource()
    {
        foreach (AudioSource source in sfxSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        AudioSource fallback = sfxSources[nextSfxSource];
        nextSfxSource = (nextSfxSource + 1) % sfxSources.Length;
        fallback.Stop();
        return fallback;
    }

    private void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
        UiVolume = PlayerPrefs.GetFloat(UiVolumeKey, DefaultUiVolume);
        IsMuted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
    }

    private void SaveAndApplySettings()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.SetFloat(UiVolumeKey, UiVolume);
        PlayerPrefs.SetInt(MutedKey, IsMuted ? 1 : 0);
        PlayerPrefs.Save();

        ApplySettings();
        SettingsChanged?.Invoke();
    }

    private void ApplySettings()
    {
        AudioListener.volume = IsMuted ? 0f : MasterVolume;

        if (musicSource != null)
        {
            musicSource.volume = musicVolumeScale * MusicVolume;
        }

        if (uiSource != null)
        {
            uiSource.volume = UiVolume;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneMusic(scene);
    }

    private void ApplySceneMusic(Scene scene)
    {
        if (EventLibrary == null || !scene.IsValid())
        {
            return;
        }

        bool isMenuScene = !string.IsNullOrEmpty(scene.name) && scene.name.StartsWith("MainMenu");
        AudioCue musicCue = isMenuScene
            ? EventLibrary.menuMusic
            : EventLibrary.gameplayMusic;

        if (musicCue?.clip != null)
        {
            PlayMusic(musicCue.clip, false, musicCue.volume);
        }
        else
        {
            StopMusic();
        }
    }
}
