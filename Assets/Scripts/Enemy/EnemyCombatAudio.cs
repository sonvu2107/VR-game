using System.Collections.Generic;
using UnityEngine;

/// <summary>Plays authored one-shot enemy ability sounds that survive their caster being destroyed.</summary>
public static class EnemyCombatAudio
{
    public enum Cue
    {
        Melee,
        Ranged,
        Poison,
        Charger,
        Summoner,
        Elite,
        MiniBoss,
        BossWave,
        BossDash,
        BossMelee,
        BossTelegraph,
        BoneRise,
        BoneImpact,
        BossDeathCollapse,
        BossSummon
    }

    private static readonly Dictionary<Cue, AudioClip> Clips = new Dictionary<Cue, AudioClip>();

    public static void PlayAttack(GameObject owner, EnemyArchetype archetype, bool special = false)
    {
        // The Skeleton King must not sound like a regular melee minion.
        if (owner != null && owner.GetComponent<SkeletonKingController>() != null)
        {
            Play(owner, Cue.BossMelee, special ? 0.94f : 0.84f,
                Random.Range(0.92f, 0.99f));
            return;
        }

        Cue cue;
        switch (archetype)
        {
            case EnemyArchetype.Ranged: cue = Cue.Ranged; break;
            case EnemyArchetype.Poison: cue = Cue.Poison; break;
            case EnemyArchetype.Charger: cue = Cue.Charger; break;
            case EnemyArchetype.Summoner: cue = Cue.Summoner; break;
            case EnemyArchetype.Elite: cue = Cue.Elite; break;
            case EnemyArchetype.MiniBoss: cue = Cue.MiniBoss; break;
            default: cue = Cue.Melee; break;
        }

        Play(owner, cue, special ? 0.94f : 0.76f, Random.Range(0.96f, 1.05f));
    }

    public static void Play(GameObject owner, Cue cue, float volume = 0.8f, float pitch = 1f)
    {
        AudioClip clip = Load(cue);
        if (clip == null) return;

        GameObject audioRoot = new GameObject("Enemy SFX - " + cue);
        if (owner != null) audioRoot.transform.position = owner.transform.position;
        AudioSource source = audioRoot.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.clip = clip;
        source.Play();
        Object.Destroy(audioRoot, clip.length / Mathf.Abs(source.pitch) + 0.15f);
        LanSessionBridge.Active?.PublishSound(clip);
    }

    private static AudioClip Load(Cue cue)
    {
        if (Clips.TryGetValue(cue, out AudioClip cached)) return cached;

        string name;
        string path = null;
        switch (cue)
        {
            case Cue.Ranged: name = "EnemyArrowShot"; break;
            case Cue.Poison: name = "PoisonCast"; break;
            case Cue.Charger: name = "ChargerBurst"; break;
            case Cue.Summoner: name = "SummonerCast"; break;
            case Cue.Elite: name = "EliteAttack"; break;
            case Cue.MiniBoss: name = "MiniBossSpecial"; break;
            case Cue.BossWave: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingWave"; name = null; break;
            case Cue.BossDash: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingDash"; name = null; break;
            case Cue.BossMelee: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingMelee"; name = null; break;
            case Cue.BossTelegraph: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingRoar"; name = null; break;
            case Cue.BoneRise: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingBoneRise"; name = null; break;
            case Cue.BoneImpact: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingBoneImpact"; name = null; break;
            case Cue.BossDeathCollapse: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingSoulDeath"; name = null; break;
            case Cue.BossSummon: path = "Audio/Combat/Enemies/SkeletonKing/SkeletonKingSummon"; name = null; break;
            default: name = "EnemyMeleeAttack"; break;
        }

        if (string.IsNullOrEmpty(path)) path = "Audio/Combat/Enemies/" + name;
        AudioClip clip = Resources.Load<AudioClip>(path);
        Clips[cue] = clip;
        return clip;
    }
}
