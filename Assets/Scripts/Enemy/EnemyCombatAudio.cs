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
        BossDash
    }

    private static readonly Dictionary<Cue, AudioClip> Clips = new Dictionary<Cue, AudioClip>();

    public static void PlayAttack(GameObject owner, EnemyArchetype archetype, bool special = false)
    {
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
        switch (cue)
        {
            case Cue.Ranged: name = "EnemyArrowShot"; break;
            case Cue.Poison: name = "PoisonCast"; break;
            case Cue.Charger: name = "ChargerBurst"; break;
            case Cue.Summoner: name = "SummonerCast"; break;
            case Cue.Elite: name = "EliteAttack"; break;
            case Cue.MiniBoss: name = "MiniBossSpecial"; break;
            case Cue.BossWave: name = "BossWave"; break;
            case Cue.BossDash: name = "BossDash"; break;
            default: name = "EnemyMeleeAttack"; break;
        }

        AudioClip clip = Resources.Load<AudioClip>("Audio/Combat/Enemies/" + name);
        Clips[cue] = clip;
        return clip;
    }
}
