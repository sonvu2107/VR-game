using UnityEngine;

/// <summary>
/// One shared cast slot for a boss.  Independent attack controllers must reserve
/// this before starting an attack so telegraphs never overlap into unfair damage.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossAttackScheduler : MonoBehaviour
{
    private float lockedUntil;
    private string owner;

    public bool IsLocked => Time.time < lockedUntil;
    public string CurrentOwner => IsLocked ? owner : string.Empty;

    public bool TryReserve(string reservationOwner, float duration)
    {
        if (IsLocked) return false;
        owner = reservationOwner;
        lockedUntil = Time.time + Mathf.Max(0.05f, duration);
        return true;
    }

    public void Release(string reservationOwner)
    {
        if (owner != reservationOwner) return;
        lockedUntil = Time.time;
        owner = string.Empty;
    }
}
