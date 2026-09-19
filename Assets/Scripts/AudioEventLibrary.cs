using System;
using UnityEngine;

public enum AudioEventId
{
    CubeMove,
    CubeMoveGlass,
    GameOver,
    LevelComplete,
    Teleport,
    Split,
    PlayerSwitch,
    SwitchActivated,
    BridgeOpen,
    BridgeClose,
    Conveyor,
    UiHover,
    UiClick,
    MenuTransition,
    Restart,
    Hint,
    Undo,
    GlassFracture,
    GlassShatter
}

[Serializable]
public sealed class AudioCue
{
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0f, 0.5f)]
    public float pitchVariation;
}

[CreateAssetMenu(fileName = "AudioEventLibrary", menuName = "BrainCube/Audio Event Library")]
public sealed class AudioEventLibrary : ScriptableObject
{
    [Header("Music")]
    public AudioCue menuMusic = new AudioCue();
    public AudioCue gameplayMusic = new AudioCue();

    [Header("Player")]
    public AudioCue cubeMove = new AudioCue();
    public AudioCue cubeMoveGlass = new AudioCue();
    public AudioCue gameOver = new AudioCue();
    public AudioCue levelComplete = new AudioCue();
    public AudioCue teleport = new AudioCue();
    public AudioCue split = new AudioCue();
    public AudioCue playerSwitch = new AudioCue();

    [Header("World")]
    public AudioCue switchActivated = new AudioCue();
    public AudioCue bridgeOpen = new AudioCue();
    public AudioCue bridgeClose = new AudioCue();
    public AudioCue conveyor = new AudioCue();
    public AudioCue glassFracture = new AudioCue();
    public AudioCue glassShatter = new AudioCue();

    [Header("UI")]
    public AudioCue uiHover = new AudioCue();
    public AudioCue uiClick = new AudioCue();
    public AudioCue menuTransition = new AudioCue();
    public AudioCue restart = new AudioCue();
    public AudioCue hint = new AudioCue();
    public AudioCue undo = new AudioCue();

    public AudioCue GetCue(AudioEventId eventId)
    {
        return eventId switch
        {
            AudioEventId.CubeMove => cubeMove,
            AudioEventId.CubeMoveGlass => cubeMoveGlass,
            AudioEventId.GameOver => gameOver,
            AudioEventId.LevelComplete => levelComplete,
            AudioEventId.Teleport => teleport,
            AudioEventId.Split => split,
            AudioEventId.PlayerSwitch => playerSwitch,
            AudioEventId.SwitchActivated => switchActivated,
            AudioEventId.BridgeOpen => bridgeOpen,
            AudioEventId.BridgeClose => bridgeClose,
            AudioEventId.Conveyor => conveyor,
            AudioEventId.UiHover => uiHover,
            AudioEventId.UiClick => uiClick,
            AudioEventId.MenuTransition => menuTransition,
            AudioEventId.Restart => restart,
            AudioEventId.Hint => hint,
            AudioEventId.Undo => undo,
            AudioEventId.GlassFracture => glassFracture,
            AudioEventId.GlassShatter => glassShatter,
            _ => null
        };
    }
}
