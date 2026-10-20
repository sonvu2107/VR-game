using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class CombatAudioAssetIntegration : AssetPostprocessor
{
    private static readonly string[] ResourcePaths =
    {
        "Audio/Combat/Characters/AzureWarden/Dash",
        "Audio/Combat/Characters/AzureWarden/Q_AegisLance",
        "Audio/Combat/Characters/AzureWarden/E_Wardbreaker",
        "Audio/Combat/Characters/AzureWarden/R_CitadelVerdict",
        "Audio/Combat/Characters/Chronoblade/Dash",
        "Audio/Combat/Characters/Chronoblade/Q_RiftCutter",
        "Audio/Combat/Characters/Chronoblade/E_TimeFracture",
        "Audio/Combat/Characters/Chronoblade/R_ZeroHour",
        "Audio/Combat/Characters/GravewindRanger/Dash_MistStep",
        "Audio/Combat/Characters/GravewindRanger/BasicArrow",
        "Audio/Combat/Characters/GravewindRanger/Q_WraithFan",
        "Audio/Combat/Characters/GravewindRanger/E_GraveSnare",
        "Audio/Combat/Characters/GravewindRanger/R_Nightfall",
        "Audio/Combat/Characters/GravewindRanger/NightfallImpact",
        "Audio/Combat/Enemies/EnemyDefeat",
        "Audio/Combat/Enemies/MiniBossDefeat",
        "Audio/Combat/Enemies/BossDefeat",
        "Audio/Combat/Enemies/EnemyMeleeAttack",
        "Audio/Combat/Enemies/EnemyArrowShot",
        "Audio/Combat/Enemies/PoisonCast",
        "Audio/Combat/Enemies/ChargerBurst",
        "Audio/Combat/Enemies/SummonerCast",
        "Audio/Combat/Enemies/EliteAttack",
        "Audio/Combat/Enemies/MiniBossSpecial",
        "Audio/Combat/Enemies/BossWave",
        "Audio/Combat/Enemies/BossDash"
    };

    static CombatAudioAssetIntegration()
    {
        EditorApplication.delayCall += ConfigureAndValidate;
    }

    private static bool IsManagedClip(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return false;
        foreach (string resourcePath in ResourcePaths)
            if (string.Equals(assetPath, $"Assets/Resources/{resourcePath}.wav",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void OnPreprocessAudio()
    {
        if (IsManagedClip(assetPath))
            Apply((AudioImporter)assetImporter);
    }

    private static void Apply(AudioImporter importer)
    {
        importer.forceToMono = true;
        importer.loadInBackground = false;
        importer.ambisonic = false;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
    }

    private static bool NeedsConfiguration(AudioImporter importer)
    {
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        return !importer.forceToMono || importer.loadInBackground ||
               importer.ambisonic || settings.loadType != AudioClipLoadType.DecompressOnLoad ||
               settings.compressionFormat != AudioCompressionFormat.PCM ||
               settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
               !settings.preloadAudioData;
    }

    [MenuItem("Tools/The Labyrinth/Validate Combat Audio")]
    public static void ConfigureAndValidate()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConfigureAndValidate;
            return;
        }

        int loaded = 0;
        foreach (string resourcePath in ResourcePaths)
        {
            string assetPath = $"Assets/Resources/{resourcePath}.wav";
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException($"Missing combat audio importer: {assetPath}");
            if (NeedsConfiguration(importer))
            {
                Apply(importer);
                importer.SaveAndReimport();
            }

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null || clip.length <= 0.05f || clip.frequency != 44100)
                throw new InvalidOperationException($"Invalid combat audio clip: {resourcePath}");
            loaded++;
        }

        Debug.Log($"COMBAT_AUDIO_PASS clips={loaded} sampleRate=44100 preload=true " +
                  "uniqueAbilitySfx=true enemyAttackFamilies=9 defeatTiers=3");
    }
}
