using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private float nextRepathTime;

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

    public bool SetDestination(Vector3 destination, float speed, float repathInterval = 0.25f, bool force = false)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        agent.speed = Mathf.Max(0.1f, speed);
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
}
