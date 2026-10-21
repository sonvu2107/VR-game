using UnityEngine;
using UnityEngine.AI;
using System.Linq;

/// <summary>Biến một prefab có sẵn thành boss, chỉ sửa config bản sao của lượt chơi.</summary>
public sealed class CampaignFinalBoss : MonoBehaviour
{
    private EnemyConfigSO runtimeConfig;
    public string SourceName { get; private set; }
    public float SizeMultiplier { get; private set; }

    // AnimationEvent của Skeleton gốc, nay đã chuyển sang EnemyCombat.
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        var source = GetComponent<AudioSource>();
        if (source != null) source.PlayOneShot(clip);
        LanSessionBridge.Active?.PublishSound(clip);
    }

    public static CampaignFinalBoss Create(GameObject prefab, DungeonRoom room, Vector3 spawn)
    {
        var staging = new GameObject("Boss staging");
        staging.SetActive(false);
        GameObject root = Instantiate(prefab, spawn, Quaternion.identity, staging.transform);
        var marker = root.AddComponent<CampaignFinalBoss>();
        marker.SourceName = prefab.name;
        root.name = "FinalBoss_" + prefab.name;
        var legacy = root.GetComponent<Enemy>();
        if (legacy != null)
        {
            legacy.SetCountsForLevelClear(false);
            legacy.enabled = false;
            if (Application.isPlaying) Destroy(legacy); else DestroyImmediate(legacy);
        }
        var health = root.GetComponent<EnemyHealth>() ?? root.AddComponent<EnemyHealth>();
        var stats = health.Config != null ? Instantiate(health.Config) : ScriptableObject.CreateInstance<EnemyConfigSO>();
        stats.name = "RunBoss_" + prefab.name;
        bool isSkeletonKing = root.GetComponent<SkeletonKingController>() != null;
        // Level 10 is the campaign finale: make every possible boss roll feel
        // meaningfully tougher without changing the source prefab's balance.
        stats.maxHealth = isSkeletonKing ? 1400 : Mathf.Max(900, stats.maxHealth * 7);
        if (isSkeletonKing) stats.displayName = "Vua Xương";
        stats.scoreValue = Mathf.Max(300, stats.scoreValue * 10);
        stats.detectionRange = 30f;
        stats.attackDamage = Mathf.Max(2, Mathf.CeilToInt(stats.attackDamage * 2f));
        stats.contactDamage = Mathf.Max(2, Mathf.CeilToInt(stats.contactDamage * 2f));
        stats.attackCooldown = Mathf.Clamp(stats.attackCooldown * 0.42f, 0.45f, 0.9f);
        stats.attackWindup = Mathf.Clamp(stats.attackWindup * 0.55f, 0.2f, 0.38f);
        stats.attackRecovery = Mathf.Clamp(stats.attackRecovery * 0.55f, 0.2f, 0.42f);
        stats.specialCooldown = Mathf.Clamp(stats.specialCooldown * 0.4f, 0.65f, 1.4f);
        // Barrel boss nổ theo hồi chiêu, không tự sát như Barrel thường.
        stats.selfDestructOnAttack = false;
        marker.runtimeConfig = stats;
        health.ConfigureAsFinalBoss(stats);
        var movement = root.GetComponent<EnemyMovement>() ?? root.AddComponent<EnemyMovement>();
        var combat = root.GetComponent<EnemyCombat>() ?? root.AddComponent<EnemyCombat>();
        combat.Configure(stats);
        var brain = root.GetComponent<EnemyBrain>();
        if (brain != null || root.GetComponent<SkeletonKingController>() == null)
        {
            if (brain == null) brain = root.AddComponent<EnemyBrain>();
            brain.Configure(stats);
        }
        root.GetComponent<EnemySummoner>()?.Configure(stats);
        var sheet = root.GetComponent<EnemySheetAnimation>();
        if (sheet != null) sheet.Renderer.sprite = sheet.Art.Frame(0, sheet.Art.idle, 0);
        if (root.GetComponent<SkeletonKingController>() != null)
            EnemyRuntimeSpriteAnimation.TryAttach(movement, root.GetComponent<SpriteRenderer>());

        // Full sprite rectangle gồm cả khoảng cho animation đánh; chừa thêm một ô né đòn.
        bool found = false;
        Bounds relative = new Bounds();
        foreach (var visual in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!visual.enabled || visual.sprite == null) continue;
            if (!found) { relative = visual.bounds; found = true; } else relative.Encapsulate(visual.bounds);
        }
        Vector3 offset = relative.center - root.transform.position;
        Vector3 initialScale = root.transform.localScale;
        Vector3 location = spawn;
        float factor = 1f;
        bool fits = false;
        // Maximum 1.75x scale while keeping the sprite bounds around 3.5 map tiles.
        float maxFactor = Mathf.Clamp(3.5f / Mathf.Max(.01f, Mathf.Max(relative.size.x, relative.size.y)), 1f, 1.75f);
        for (float test = maxFactor; test >= .99f && !fits; test = test > 1f ? Mathf.Max(1f, test - .25f) : 0f)
        foreach (var tile in room.FloorTiles.OrderBy(p => ((Vector2)p - (Vector2)spawn).sqrMagnitude).ThenBy(p => p.y).ThenBy(p => p.x))
        {
            Vector3 point = new Vector3(tile.x, tile.y + 1f, spawn.z);
            Vector3 center = point + offset * test;
            Vector3 extents = relative.extents * test + Vector3.one;
            bool safe = true;
            for (int x = Mathf.FloorToInt(center.x - extents.x); x <= Mathf.CeilToInt(center.x + extents.x) && safe; x++)
            for (int y = Mathf.FloorToInt(center.y - extents.y - 1f); y <= Mathf.CeilToInt(center.y + extents.y - 1f); y++)
                if (!room.FloorTiles.Contains(new Vector2Int(x, y))) { safe = false; break; }
            if (!safe) continue;
            NavMeshHit hit;
            if (Application.isPlaying && !NavMesh.SamplePosition(point, out hit, 0.25f, NavMesh.AllAreas)) continue;
            factor = test; location = point; fits = true; break;
        }
        root.transform.localScale = initialScale * factor;
        root.transform.position = location;
        marker.SizeMultiplier = factor;
        // Đòn cận chiến phải chạm được người chơi đứng ngoài thân boss lớn.
        stats.attackRange = Mathf.Max(stats.attackRange, Mathf.Max(relative.extents.x, relative.extents.y) * factor + 0.6f);
        if (stats.blastRadius > 0f) stats.blastRadius = Mathf.Max(stats.blastRadius, 2f);
        root.transform.SetParent(null, true);
        root.SetActive(true);
        if (root.GetComponent<FinalBossHealthBar>() == null)
            root.AddComponent<FinalBossHealthBar>();
        if (Application.isPlaying) Destroy(staging); else DestroyImmediate(staging);
        movement.ConfigureBossArena(room.FloorTiles, (Vector2)(relative.extents * factor + new Vector3(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) * factor));
        Debug.Log($"FINAL_BOSS_SELECTED source={prefab.name} multiplier={factor} hp={stats.maxHealth} damage={stats.attackDamage} cooldown={stats.attackCooldown:F2}");
        return marker;
    }

    private void OnDestroy()
    {
        if (runtimeConfig == null) return;
        if (Application.isPlaying) Destroy(runtimeConfig); else DestroyImmediate(runtimeConfig);
    }
}
