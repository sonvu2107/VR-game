using UnityEngine;

/// <summary>
/// Boundary between the core game loop and a LAN transport implementation.
/// A future Netcode bridge should forward client requests to the Host and
/// publish authoritative state changes to connected clients.
/// </summary>
public abstract class GameSessionBridge : MonoBehaviour
{
    public abstract bool HasStateAuthority { get; }

    public abstract void RequestLevelAdvance(int currentLevel);

    public virtual void PublishState(GameState state)
    {
    }

    public virtual void PublishLevel(int currentLevel, int totalLevels, int levelSeed)
    {
    }
}
