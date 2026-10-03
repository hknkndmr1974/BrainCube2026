using System;
using UnityEngine;

public enum FallReason
{
    Edge,
    FragileTile
}

/// <summary>
/// Oyun içi anlık olaylar (tutorial ve ipuçları için).
/// Simülasyon ve menü dekor küpü bu olayları tetiklemez.
/// </summary>
public static class GameplayEvents
{
    /// <summary>Oyuncunun kendi girdisiyle (kaydırma/klavye) yaptığı hamle.</summary>
    public static event Action<Vector3> PlayerMoved;
    public static event Action<FallReason> PlayerFell;
    public static event Action GoalReachedLying;
    public static event Action HardSwitchPressedLying;

    public static void RaisePlayerMoved(Vector3 direction) => PlayerMoved?.Invoke(direction);
    public static void RaisePlayerFell(FallReason reason) => PlayerFell?.Invoke(reason);
    public static void RaiseGoalReachedLying() => GoalReachedLying?.Invoke();
    public static void RaiseHardSwitchPressedLying() => HardSwitchPressedLying?.Invoke();
}
