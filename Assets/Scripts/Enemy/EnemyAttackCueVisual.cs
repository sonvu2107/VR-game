using System.Collections;
using UnityEngine;

/// <summary>Visible warning ring shown at the target while an enemy attack winds up.</summary>
[DisallowMultipleComponent]
public sealed class EnemyAttackCueVisual : MonoBehaviour
{
    private SpriteRenderer visual;
    private float duration;
    private Color color;

    public static void Show(EnemyCombat owner, HealthController target, float windup, bool special)
    {
        if (owner == null || target == null || windup < 0.12f) return;
        GameObject cue = new("Enemy Attack Telegraph");
        cue.transform.position = new Vector3(target.transform.position.x, target.transform.position.y, 0.04f);
        EnemyAttackCueVisual effect = cue.AddComponent<EnemyAttackCueVisual>();
        effect.visual = cue.AddComponent<SpriteRenderer>();
        effect.visual.sprite = EnemyEffectSpriteFactory.Ring;
        effect.visual.sortingOrder = 16;
        effect.duration = Mathf.Clamp(windup, 0.18f, 0.8f);
        effect.color = ResolveColor(owner, special);
        effect.visual.color = effect.color;
        cue.transform.localScale = Vector3.one * 0.4f;
        effect.StartCoroutine(effect.Play());
    }

    private IEnumerator Play()
    {
        float elapsed = 0f;
        Vector3 small = Vector3.one * 0.4f;
        Vector3 large = Vector3.one * 1.35f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = 0.88f + 0.12f * Mathf.Sin(t * Mathf.PI * 5f);
            transform.localScale = Vector3.Lerp(small, large, t) * pulse;
            visual.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.9f, 0.3f, t));
            yield return null;
        }
        Destroy(gameObject);
    }

    private static Color ResolveColor(EnemyCombat owner, bool special)
    {
        if (owner.GetComponent<SkeletonKingController>() != null || owner.GetComponent<MiniBossController>() != null)
            return new Color(1f, 0.18f, 0.12f);
        EnemyArchetype archetype = owner.GetComponent<EnemyBrain>()?.Config != null
            ? owner.GetComponent<EnemyBrain>().Config.archetype : EnemyArchetype.Melee;
        if (archetype == EnemyArchetype.Poison) return new Color(0.2f, 1f, 0.35f);
        if (archetype == EnemyArchetype.Ranged || archetype == EnemyArchetype.Summoner)
            return new Color(0.5f, 0.35f, 1f);
        if (archetype == EnemyArchetype.Charger || special) return new Color(1f, 0.46f, 0.08f);
        return new Color(1f, 0.84f, 0.15f);
    }
}
