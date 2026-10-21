// Di chuyển theo NavMeshAgent, giữ agent đồng bộ vị trí với hình ảnh 2D.
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private float nextRepathTime;
    private float crowdControlUntil = -1f;
    private float crowdControlSpeedMultiplier = 1f;
    private readonly List<Vector3> bossArenaCells = new List<Vector3>();

    public Vector2 Velocity => agent != null ? agent.velocity : Vector2.zero;
    public bool IsStopped => agent == null || agent.isStopped || !agent.isOnNavMesh;

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.autoBraking = true;
        }
    }

    private void Start()
    {
        if (GetComponent<EnemyHealth>() == null || GetComponent<EnemyLocomotionVisual>() != null ||
            GetComponent<EnemySpriteAnimation>() != null || GetComponent<EnemySheetAnimation>() != null ||
            GetComponent<EnemyRuntimeSpriteAnimation>() != null)
            return;

        if (EnemyRuntimeSpriteAnimation.TryAttach(this, spriteRenderer))
            return;

        gameObject.AddComponent<EnemyLocomotionVisual>().Initialize(this);
    }

    public void SetVisualRenderer(SpriteRenderer renderer)
    {
        if (renderer != null) spriteRenderer = renderer;
    }

    public bool SetDestination(Vector3 destination, float speed, float repathInterval = 0.25f, bool force = false)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if (Time.time >= crowdControlUntil)
            crowdControlSpeedMultiplier = 1f;
        else if (crowdControlSpeedMultiplier <= 0.01f)
        {
            Stop();
            return false;
        }
        if (bossArenaCells.Count > 0)
            destination = NearestBossArenaCell(destination);
        agent.speed = Mathf.Max(0.1f, speed * crowdControlSpeedMultiplier);
        agent.isStopped = false;
        if (!force && Time.time < nextRepathTime) return true;
        nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;
        return agent.SetPath(path);
    }

    public bool TrySetNearbyDestination(Vector3 desiredPosition, float sampleRadius, float speed, float repathInterval = 0.25f)
    {
        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return false;
        return SetDestination(hit.position, speed, repathInterval, true);
    }

    public bool TryWander(float radius, float speed, float repathInterval)
    {
        Vector2 randomOffset = Random.insideUnitCircle * radius;
        Vector3 desired = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
        return TrySetNearbyDestination(desired, radius, speed, repathInterval);
    }

    public bool TryRetreatFrom(Vector3 threat, float distance, float speed, float repathInterval)
    {
        Vector2 away = (Vector2)(transform.position - threat);
        if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle.normalized;
        Vector3 desired = transform.position + (Vector3)(away.normalized * distance);
        return TrySetNearbyDestination(desired, distance, speed, repathInterval);
    }

    public void Stop()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    /// <summary>Restrict a large boss to floor tiles with room for its visible sprite.</summary>
    public void ConfigureBossArena(HashSet<Vector2Int> floorTiles, Vector2 visualExtents)
    {
        bossArenaCells.Clear();
        if (floorTiles == null || floorTiles.Count == 0) return;

        int requiredX = Mathf.Max(1, Mathf.CeilToInt(visualExtents.x + 0.5f));
        int requiredY = Mathf.Max(1, Mathf.CeilToInt(visualExtents.y + 0.5f));
        // If a random room is narrower than the artwork, retain the largest
        // interior that exists instead of leaving the boss unconstrained.
        for (int margin = Mathf.Max(requiredX, requiredY); margin >= 1; margin--)
        {
            int radiusX = Mathf.Min(requiredX, margin);
            int radiusY = Mathf.Min(requiredY, margin);
            foreach (Vector2Int cell in floorTiles)
            {
                bool fits = true;
                for (int x = -radiusX; x <= radiusX && fits; x++)
                for (int y = -radiusY; y <= radiusY; y++)
                    if (!floorTiles.Contains(cell + new Vector2Int(x, y)))
                    {
                        fits = false;
                        break;
                    }
                if (fits) bossArenaCells.Add(new Vector3(cell.x, cell.y + 1f, transform.position.z));
            }
            if (bossArenaCells.Count > 0) break;
        }

        if (bossArenaCells.Count == 0)
        {
            Debug.LogWarning($"Boss room has no safe interior tiles for {name}.", this);
            return;
        }
        Vector3 safeSpawn = NearestBossArenaCell(transform.position);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(safeSpawn);
        else
            transform.position = safeSpawn;
    }

    private Vector3 NearestBossArenaCell(Vector3 desired)
    {
        Vector3 best = bossArenaCells[0];
        float bestDistance = float.PositiveInfinity;
        foreach (Vector3 cell in bossArenaCells)
        {
            float distance = (cell - desired).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = cell;
        }
        return best;
    }

    private void LateUpdate()
    {
        if (bossArenaCells.Count == 0 || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;
        Vector3 safePosition = NearestBossArenaCell(transform.position);
        if ((safePosition - transform.position).sqrMagnitude > 1.5f * 1.5f)
        {
            agent.ResetPath();
            agent.Warp(safePosition);
        }
    }

    public void StopImmediately()
    {
        Stop();
        if (agent != null) agent.enabled = false;
    }

    public void FaceTarget(Vector3 target)
    {
        if (spriteRenderer == null) return;
        float dx = target.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f) spriteRenderer.flipX = dx < 0f;
    }

    public void SetSpeed(float speed)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.speed = Mathf.Max(0.1f, speed);
    }

    public void Resume(float speed)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.speed = Mathf.Max(0.1f, speed);
        agent.isStopped = false;
    }

    /// <summary>Moves a boss through a brief, authored charge without calculating a new path.</summary>
    public void MoveDirect(Vector3 worldDelta)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.Move(worldDelta);
            return;
        }
        transform.position += worldDelta;
    }

    /// <summary>
    /// Picks a forward charge endpoint that remains inside the configured boss arena.
    /// This prevents LateUpdate from snapping a charging boss back toward its start.
    /// </summary>
    public bool TryGetForwardChargeDestination(Vector3 origin, Vector2 direction,
        float maximumDistance, out Vector3 destination)
    {
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        maximumDistance = Mathf.Max(0.5f, maximumDistance);
        destination = origin;

        if (bossArenaCells.Count > 0)
        {
            float bestScore = float.NegativeInfinity;
            foreach (Vector3 cell in bossArenaCells)
            {
                Vector2 offset = (Vector2)(cell - origin);
                float forward = Vector2.Dot(offset, direction);
                float lateral = Mathf.Abs(direction.x * offset.y - direction.y * offset.x);
                if (forward < 0.75f || forward > maximumDistance + 0.75f || lateral > 1.15f)
                    continue;

                float score = forward - lateral * 0.45f;
                if (score <= bestScore) continue;
                bestScore = score;
                destination = cell;
            }
            return bestScore > float.NegativeInfinity;
        }

        Vector3 desired = origin + (Vector3)(direction * maximumDistance);
        if (agent != null && agent.enabled && agent.isOnNavMesh &&
            NavMesh.SamplePosition(desired, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            destination = hit.position;
        else destination = desired;
        return Vector2.Dot((Vector2)(destination - origin), direction) > 0.5f;
    }

    /// <summary>Temporarily slows or stops this enemy without disabling its NavMeshAgent.</summary>
    public void ApplyCrowdControl(float duration, float speedMultiplier)
    {
        if (duration <= 0f)
            return;
        if (Time.time >= crowdControlUntil)
            crowdControlSpeedMultiplier = 1f;

        crowdControlUntil = Mathf.Max(crowdControlUntil, Time.time + duration);
        crowdControlSpeedMultiplier = Mathf.Min(crowdControlSpeedMultiplier,
            Mathf.Clamp01(speedMultiplier));
        if (crowdControlSpeedMultiplier <= 0.01f)
            Stop();
        else if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.speed = Mathf.Max(0.1f, agent.speed * crowdControlSpeedMultiplier);
    }
}
