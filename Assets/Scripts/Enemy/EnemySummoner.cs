using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemySummoner : MonoBehaviour
{
    [SerializeField] private EnemyConfigSO config;
    private readonly List<GameObject> activeSummons = new List<GameObject>();
    private float nextSummonTime;

    public void Configure(EnemyConfigSO enemyConfig) => config = enemyConfig;

    public bool TrySummon()
    {
        if (config == null || config.summonPrefab == null || Time.time < nextSummonTime) return false;
        activeSummons.RemoveAll(item => item == null);
        if (activeSummons.Count >= config.maxAliveSummons) return false;

        Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(1f, 2f);
        Vector3 desired = transform.position + (Vector3)offset;
        if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;

        GameObject summon = Instantiate(config.summonPrefab, hit.position, Quaternion.identity);
        activeSummons.Add(summon);
        nextSummonTime = Time.time + config.specialCooldown;
        EnemyHealth summonedHealth = summon.GetComponent<EnemyHealth>();
        if (summonedHealth != null)
        {
            summonedHealth.SetCountsForLegacyCounter(false);
            summonedHealth.Died += OnSummonDied;
        }
        return true;
    }

    public void DespawnSummons()
    {
        foreach (GameObject summon in activeSummons)
            if (summon != null) Destroy(summon);
        activeSummons.Clear();
    }

    private void OnSummonDied(EnemyHealth deadSummon)
    {
        if (deadSummon != null) deadSummon.Died -= OnSummonDied;
        activeSummons.RemoveAll(item => item == null || item == deadSummon.gameObject);
    }
}
