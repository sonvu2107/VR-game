using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Creates editable Enemy/Boss configs and migrates the supplied Skeleton art prefab.</summary>
public static class EnemyBossAuthoring
{
    private const string PrefabFolder = "Assets/Prefabs/EnemyBoss";
    private const string ConfigFolder = "Assets/Scripts/ScriptableObjects/Enemies";
    private const string SkeletonSource = "Assets/Prefabs/Skeleton.prefab";
    private const string DungeonScene = "Assets/Scenes/Dungeon.unity";
    private const string PlaytestScene = "Assets/Scenes/EnemyBossPlaytest.unity";
    private const string ArtFolder = "Assets/Art/Enemies/TinyCreatures";

    [InitializeOnLoadMethod]
    private static void InstallDistinctSpritesOnce()
    {
        string installKey = "TheLabyrinth.EnemySpriteInstall." + Application.dataPath;
        if (EditorPrefs.GetBool(installKey, false)) return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/SkeletonArcher.png") == null) return;
            BuildAll();
            EditorPrefs.SetBool(installKey, true);
        };
    }

    [MenuItem("Tools/Enemy Boss/Build Configs and Prefabs")]
    public static void BuildAll()
    {
        AssetDatabase.Refresh();
        ConfigureEnemySpriteImports();
        AssetDatabase.Refresh();
        EnsureFolder("Assets/Scripts/ScriptableObjects");
        EnsureFolder(ConfigFolder);
        EnsureFolder(PrefabFolder);

        EnemyConfigSO warrior = CreateConfig("SkeletonWarrior", "Skeleton Warrior", EnemyArchetype.Melee,
            100, 1, 2.5f, 1f, 6f, 1.1f, 1.1f, 1.4f, 0.35f, 0.55f, 5f, 3f, 5f, 1.5f, 7f, 0.45f, 4f, 1f, 3);
        EnemyConfigSO archer = CreateConfig("SkeletonArcher", "Skeleton Archer", EnemyArchetype.Ranged,
            65, 1, 2.1f, 0.8f, 8f, 5f, 5f, 1.8f, 0.5f, 0.65f, 5.5f, 3.5f, 4f, 2f, 6f, 0.4f, 4f, 1f, 3);
        EnemyConfigSO slime = CreateConfig("PoisonSlime", "Slime độc", EnemyArchetype.Poison,
            90, 1, 1.5f, 0.65f, 5f, 1.1f, 1.1f, 2f, 0.4f, 0.65f, 4f, 2f, 4f, 3f, 4f, 0.4f, 5f, 1f, 4);
        EnemyConfigSO bat = CreateConfig("Bat", "Bat", EnemyArchetype.Charger,
            45, 1, 3f, 1.2f, 7f, 1.2f, 1.2f, 2f, 0.35f, 0.6f, 5f, 3f, 3f, 2.2f, 9f, 0.35f, 3f, 1f, 2);
        EnemyConfigSO mage = CreateConfig("DarkMage", "Dark Mage", EnemyArchetype.Summoner,
            75, 1, 1.8f, 0.8f, 8f, 4.5f, 4.5f, 2.2f, 0.55f, 0.7f, 6f, 3.5f, 5f, 5f, 5f, 0.4f, 4f, 1f, 5);
        EnemyConfigSO elite = CreateConfig("EliteEnemy", "Elite Enemy", EnemyArchetype.Elite,
            180, 2, 2.7f, 0.9f, 8f, 1.5f, 1.5f, 1.6f, 0.45f, 0.7f, 7f, 3.5f, 4f, 3f, 8f, 0.5f, 4f, 1f, 8);
        EnemyConfigSO miniBoss = CreateConfig("BoneSentinel", "Bone Sentinel", EnemyArchetype.MiniBoss,
            500, 2, 2.2f, 0.8f, 10f, 1.5f, 1.5f, 2f, 0.5f, 0.8f, 7f, 3.5f, 6f, 5f, 8f, 0.6f, 5f, 1f, 25);
        EnemyConfigSO king = CreateConfig("SkeletonKing", "Skeleton King", EnemyArchetype.Melee,
            1200, 3, 2.4f, 0.8f, 12f, 1.5f, 1.5f, 1.7f, 0.55f, 0.85f, 7f, 3.5f, 4f, 5f, 8f, 0.6f, 5f, 1f, 100);

        GameObject warriorPrefab = BuildPrefab("Skeleton.prefab", warrior, EnemyHealth.DeathKind.Enemy, false, false, false, new Color(1f, 1f, 1f));
        warrior.summonPrefab = warriorPrefab;
        archer.projectilePrefab = null;
        BuildPrefab("SkeletonArcher.prefab", archer, EnemyHealth.DeathKind.Enemy, false, false, false, Color.white);
        BuildPrefab("PoisonSlime.prefab", slime, EnemyHealth.DeathKind.Enemy, false, false, false, Color.white);
        BuildPrefab("Bat.prefab", bat, EnemyHealth.DeathKind.Enemy, false, false, false, Color.white);
        mage.summonPrefab = warriorPrefab;
        BuildPrefab("DarkMage.prefab", mage, EnemyHealth.DeathKind.Enemy, true, false, false, Color.white);
        BuildPrefab("EliteEnemy.prefab", elite, EnemyHealth.DeathKind.Enemy, false, false, false, Color.white);
        miniBoss.summonPrefab = warriorPrefab;
        BuildPrefab("BoneSentinel.prefab", miniBoss, EnemyHealth.DeathKind.MiniBoss, true, true, false, Color.white);

        king.summonPrefab = warriorPrefab;
        king.maxAliveSummons = 4;
        BossPhaseConfigSO phases = CreateBossPhases();
        BuildPrefab("SkeletonKing.prefab", king, EnemyHealth.DeathKind.Boss, true, false, true, Color.white, phases);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Enemy/Boss configs and prefabs created with distinct sprites. The original animated Skeleton Warrior is unchanged.");
    }

    [MenuItem("Tools/Enemy Boss/Validate Prefabs and Death")]
    public static void ValidatePrefabsAndDeath()
    {
        string[] paths =
        {
            SkeletonSource,
            PrefabFolder + "/SkeletonArcher.prefab",
            PrefabFolder + "/PoisonSlime.prefab",
            PrefabFolder + "/Bat.prefab",
            PrefabFolder + "/DarkMage.prefab",
            PrefabFolder + "/EliteEnemy.prefab",
            PrefabFolder + "/BoneSentinel.prefab",
            PrefabFolder + "/SkeletonKing.prefab"
        };

        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.InvalidOperationException("Missing generated prefab: " + path);
            if (prefab.GetComponent<EnemyHealth>() == null || prefab.GetComponent<EnemyMovement>() == null || prefab.GetComponent<EnemyCombat>() == null)
                throw new System.InvalidOperationException("Prefab missing required health/movement/combat components: " + path);
            if (prefab.GetComponent<Enemy>() != null)
                throw new System.InvalidOperationException("Legacy AI still present and would conflict: " + path);
            if (path.EndsWith("SkeletonKing.prefab") &&
                (prefab.GetComponent<SkeletonKingController>() == null || prefab.GetComponent<EnemyBrain>() != null))
                throw new System.InvalidOperationException("Skeleton King has missing controller or duplicate basic AI.");
            if (path.EndsWith("BoneSentinel.prefab") && prefab.GetComponent<MiniBossController>() == null)
                throw new System.InvalidOperationException("Bone Sentinel is missing its mini-boss controller.");
        }

        EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
        config.maxHealth = 10;
        GameObject probe = new GameObject("EnemyHealthSmokeCheck");
        try
        {
            EnemyHealth health = probe.AddComponent<EnemyHealth>();
            health.Configure(config);
            int deathEvents = 0;
            health.Died += _ => deathEvents++;
            health.TakeDamage(100);
            health.TakeDamage(1);
            if (!health.IsDead || health.CurrentHealth != 0 || deathEvents != 1)
                throw new System.InvalidOperationException("EnemyHealth smoke check failed idempotent death assertion.");
        }
        finally
        {
            Object.DestroyImmediate(probe);
            Object.DestroyImmediate(config);
        }

        Debug.Log("Enemy/Boss validation passed: 8 prefabs have required components, no duplicate legacy AI, and EnemyHealth death event fired exactly once.");
    }

    [MenuItem("Tools/Enemy Boss/Create Playtest Scene")]
    public static void CreatePlaytestScene()
    {
        Scene previouslyOpen = SceneManager.GetActiveScene();
        if (previouslyOpen.IsValid() && previouslyOpen.isDirty)
            throw new System.InvalidOperationException("Save the currently open scene before creating the Enemy/Boss playtest scene.");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlaytestScene) == null &&
            !AssetDatabase.CopyAsset(DungeonScene, PlaytestScene))
            throw new System.InvalidOperationException("Could not copy Dungeon scene for playtest.");

        Scene scene = EditorSceneManager.OpenScene(PlaytestScene, OpenSceneMode.Single);
        GameObject harness = GameObject.Find("EnemyBossPlaytest");
        if (harness == null) harness = new GameObject("EnemyBossPlaytest");
        EnemyBossPlaytestSpawner spawner = harness.GetComponent<EnemyBossPlaytestSpawner>();
        if (spawner == null) spawner = harness.AddComponent<EnemyBossPlaytestSpawner>();

        GameObject player = GameObject.Find("Player");
        if (player == null) throw new System.InvalidOperationException("Dungeon copy has no Player object.");
        SetObject(spawner, "player", player.transform);

        string[] paths =
        {
            SkeletonSource,
            PrefabFolder + "/SkeletonArcher.prefab",
            PrefabFolder + "/PoisonSlime.prefab",
            PrefabFolder + "/Bat.prefab",
            PrefabFolder + "/DarkMage.prefab",
            PrefabFolder + "/EliteEnemy.prefab",
            PrefabFolder + "/BoneSentinel.prefab",
            PrefabFolder + "/SkeletonKing.prefab"
        };
        SerializedObject serialized = new SerializedObject(spawner);
        SerializedProperty prefabs = serialized.FindProperty("prefabs");
        prefabs.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab == null) throw new System.InvalidOperationException("Build prefabs first; missing " + paths[i]);
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (previouslyOpen.IsValid() && !string.IsNullOrEmpty(previouslyOpen.path) && previouslyOpen.path != PlaytestScene)
            EditorSceneManager.OpenScene(previouslyOpen.path, OpenSceneMode.Single);
        Debug.Log("Enemy/Boss playtest scene ready: " + PlaytestScene + ". Open it and press 1-8 to spawn each enemy or boss.");
    }

    private static EnemyConfigSO CreateConfig(string file, string display, EnemyArchetype type,
        int hp, int damage, float chase, float wander, float detection, float range, float preferred,
        float cooldown, float windup, float recovery, float projectileSpeed, float projectileLife,
        float specialCooldown, float specialRange, float dashSpeed, float dashDuration,
        float poisonDuration, float poisonTick, int summons)
    {
        string path = ConfigFolder + "/" + file + ".asset";
        EnemyConfigSO asset = AssetDatabase.LoadAssetAtPath<EnemyConfigSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<EnemyConfigSO>();
            AssetDatabase.CreateAsset(asset, path);
        }
        asset.displayName = display;
        asset.archetype = type;
        asset.maxHealth = hp;
        asset.contactDamage = damage;
        asset.chaseSpeed = chase;
        asset.wanderSpeed = wander;
        asset.detectionRange = detection;
        asset.attackRange = range;
        asset.preferredRange = preferred;
        asset.attackDamage = damage;
        asset.attackCooldown = cooldown;
        asset.attackWindup = windup;
        asset.attackRecovery = recovery;
        asset.projectileSpeed = projectileSpeed;
        asset.projectileLifetime = projectileLife;
        asset.specialCooldown = specialCooldown;
        asset.specialRange = specialRange;
        asset.dashSpeed = dashSpeed;
        asset.dashDuration = dashDuration;
        asset.poisonDuration = poisonDuration;
        asset.poisonTickInterval = poisonTick;
        asset.maxAliveSummons = summons;
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static BossPhaseConfigSO CreateBossPhases()
    {
        const string path = ConfigFolder + "/SkeletonKingPhases.asset";
        BossPhaseConfigSO phases = AssetDatabase.LoadAssetAtPath<BossPhaseConfigSO>(path);
        if (phases == null)
        {
            phases = ScriptableObject.CreateInstance<BossPhaseConfigSO>();
            AssetDatabase.CreateAsset(phases, path);
        }
        phases.phases = new[]
        {
            new BossPhaseDefinition { healthThreshold = 1f, moveSpeed = 2.4f, attackCooldown = 1.8f },
            new BossPhaseDefinition { healthThreshold = 0.65f, moveSpeed = 2.8f, attackCooldown = 1.5f, summonSkeletons = true, createHazards = true },
            new BossPhaseDefinition { healthThreshold = 0.30f, moveSpeed = 3.3f, attackCooldown = 1.15f, createHazards = true, fireSwordWaves = true, repositionAfterAttack = true }
        };
        phases.transitionDuration = 1.25f;
        phases.maximumSummons = 4;
        phases.hazardCooldown = 6f;
        phases.waveSpeed = 6f;
        EditorUtility.SetDirty(phases);
        return phases;
    }

    private static GameObject BuildPrefab(string outputName, EnemyConfigSO config, EnemyHealth.DeathKind deathKind,
        bool withSummoner, bool withMiniBoss, bool withKing, Color tint, BossPhaseConfigSO phases = null)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SkeletonSource);
        try
        {
            Enemy legacy = root.GetComponent<Enemy>();
            if (legacy != null) Object.DestroyImmediate(legacy, true);
            if (withKing)
            {
                EnemyBrain oldBrain = root.GetComponent<EnemyBrain>();
                if (oldBrain != null) Object.DestroyImmediate(oldBrain, true);
            }
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            if (body != null) body.bodyType = RigidbodyType2D.Kinematic;
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.updateRotation = false;
                agent.updateUpAxis = false;
                agent.speed = config.wanderSpeed;
                agent.radius = 0.18f;
                agent.stoppingDistance = Mathf.Min(0.5f, config.attackRange * 0.5f);
            }

            Animator animator = root.GetComponent<Animator>();
            SpriteRenderer sprite = root.GetComponent<SpriteRenderer>();
            Sprite customSprite = LoadEnemySprite(outputName);
            if (sprite != null)
            {
                if (customSprite != null) sprite.sprite = customSprite;
                sprite.color = tint;
            }
            // Existing Skeleton clips keyframe SpriteRenderer.sprite. Only the original
            // Warrior keeps that Animator; otherwise the clips overwrite the new art.
            if (animator != null) animator.enabled = outputName == "Skeleton.prefab";
            Transform attackPoint = root.transform.Find("AttackPoint");
            EnemyHealth health = GetOrAdd<EnemyHealth>(root);
            EnemyMovement movement = GetOrAdd<EnemyMovement>(root);
            EnemyCombat combat = GetOrAdd<EnemyCombat>(root);
            EnemySummoner summoner = withSummoner ? GetOrAdd<EnemySummoner>(root) : root.GetComponent<EnemySummoner>();
            EnemyBrain brain = withKing ? null : GetOrAdd<EnemyBrain>(root);
            MiniBossController miniBoss = withMiniBoss ? GetOrAdd<MiniBossController>(root) : root.GetComponent<MiniBossController>();
            SkeletonKingController king = withKing ? GetOrAdd<SkeletonKingController>(root) : root.GetComponent<SkeletonKingController>();

            SetObject(health, "config", config); SetObject(health, "animator", animator); SetEnum(health, "deathKind", (int)deathKind);
            SetObject(combat, "config", config); SetObject(combat, "health", health); SetObject(combat, "movement", movement);
            SetObject(combat, "animator", animator); SetMask(combat, "playerLayers", LayerMask.GetMask("Player"));
            SetMask(combat, "wallLayers", EnemyCombat.ResolveWallLayers().value); SetObject(combat, "attackPoint", attackPoint);
            if (brain != null)
            {
                SetObject(brain, "config", config); SetObject(brain, "health", health); SetObject(brain, "movement", movement);
                SetObject(brain, "combat", combat); SetObject(brain, "summoner", summoner); SetObject(brain, "animator", animator);
            }
            if (summoner != null) SetObject(summoner, "config", config);
            if (miniBoss != null)
            {
                SetObject(miniBoss, "health", health); SetObject(miniBoss, "summoner", summoner);
                SetObject(miniBoss, "combat", combat); SetObject(miniBoss, "config", config);
            }
            if (king != null)
            {
                SetObject(king, "phaseConfig", phases); SetObject(king, "health", health); SetObject(king, "movement", movement);
                SetObject(king, "combat", combat); SetObject(king, "summoner", summoner); SetObject(king, "animator", animator);
                SetObject(king, "spriteRenderer", sprite);
            }

            string output = outputName == "Skeleton.prefab" ? SkeletonSource : PrefabFolder + "/" + outputName;
            return PrefabUtility.SaveAsPrefabAsset(root, output);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void ConfigureEnemySpriteImports()
    {
        SetSpriteImport("SkeletonArcher.png", 1024f, 256);
        SetSpriteImport("BoneSentinel.png", 512f, 256);
        SetSpriteImport("SkeletonKing.png", 512f, 256);
        SetSpriteImport("PoisonSlime.png", 64f, 32);
        SetSpriteImport("Bat.png", 64f, 32);
        SetSpriteImport("DarkMage.png", 64f, 32);
        SetSpriteImport("EliteKnight.png", 64f, 32);
    }

    private static void SetSpriteImport(string file, float pixelsPerUnit, int maxSize)
    {
        string path = ArtFolder + "/" + file;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = maxSize;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static Sprite LoadEnemySprite(string prefabName)
    {
        string artFile = null;
        switch (prefabName)
        {
            case "SkeletonArcher.prefab": artFile = "SkeletonArcher.png"; break;
            case "PoisonSlime.prefab": artFile = "PoisonSlime.png"; break;
            case "Bat.prefab": artFile = "Bat.png"; break;
            case "DarkMage.prefab": artFile = "DarkMage.png"; break;
            case "EliteEnemy.prefab": artFile = "EliteKnight.png"; break;
            case "BoneSentinel.prefab": artFile = "BoneSentinel.png"; break;
            case "SkeletonKing.prefab": artFile = "SkeletonKing.png"; break;
        }
        return string.IsNullOrEmpty(artFile) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + artFile);
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        if (target == null) return;
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) { Debug.LogError($"Missing serialized field {propertyName} on {target.GetType().Name}"); return; }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetMask(Object target, string propertyName, int mask)
    {
        if (target == null) return;
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).intValue = mask;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(Object target, string propertyName, int value)
    {
        if (target == null) return;
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).enumValueIndex = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
