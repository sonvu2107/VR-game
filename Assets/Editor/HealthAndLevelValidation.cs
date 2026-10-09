using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HealthAndLevelValidation
{
    [MenuItem("Tools/Validate Health And Enemy Levels")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        InfiniteWorldGenerator generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        HealthController playerHealth = UnityEngine.Object.FindObjectOfType<HealthController>();
        Require(manager != null && generator != null && playerHealth != null,
            "Dungeon scene needs GameManager, generator and player health.");
        Require(playerHealth.startHeart == 5 && playerHealth.heartImages.Length >= 5,
            "Player must start with five visible hearts.");

        SerializedObject campaign = new(manager);
        SerializedProperty levels = campaign.FindProperty("levels");
        Require(levels != null && levels.arraySize == 10, "Campaign must contain ten levels.");
        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(i);
            int rooms = level.FindPropertyRelative("roomCount").intValue;
            Require(rooms is 6 or 7, $"Level {i + 1} must have six or seven rooms.");
            int type = level.FindPropertyRelative("levelType").enumValueIndex;
            if (i == 4 || i == 8) Require(type == (int)LevelType.MiniBoss, "Mini-boss level is missing.");
            if (i == 9) Require(type == (int)LevelType.FinalBoss, "Final boss level is missing.");
            LevelType levelType = (LevelType)type;
            int combatRooms = rooms - 4;
            int normalEnemies = InfiniteWorldGenerator.GetRoomEnemyQuota(RoomType.Start, levelType) +
                combatRooms * InfiniteWorldGenerator.GetRoomEnemyQuota(RoomType.Combat, levelType) +
                InfiniteWorldGenerator.GetRoomEnemyQuota(RoomType.Treasure, levelType) +
                InfiniteWorldGenerator.GetRoomEnemyQuota(RoomType.Boss, levelType) +
                InfiniteWorldGenerator.GetRoomEnemyQuota(RoomType.Exit, levelType);
            Require(normalEnemies is >= 8 and <= 12,
                $"Level {i + 1} should spread a moderate number of enemies across rooms.");
        }

        SerializedObject spawner = new(generator);
        string[] normalEnemyFields =
        {
            "skeletonArcherPrefab", "poisonSlimePrefab", "batPrefab", "darkMagePrefab", "eliteEnemyPrefab"
        };
        foreach (string field in normalEnemyFields)
            RequireEnemyPrefab(spawner, field);
        RequireEnemyPrefab(spawner, "miniBossPrefab");
        RequireEnemyPrefab(spawner, "bossPrefab");
        CheckGuaranteedEnemy(generator, spawner, 3, "skeletonArcherPrefab");
        CheckGuaranteedEnemy(generator, spawner, 4, "poisonSlimePrefab");
        CheckGuaranteedEnemy(generator, spawner, 6, "batPrefab");
        CheckGuaranteedEnemy(generator, spawner, 7, "eliteEnemyPrefab");
        CheckGuaranteedEnemy(generator, spawner, 8, "darkMagePrefab");

        GameObject archer = spawner.FindProperty("skeletonArcherPrefab").objectReferenceValue as GameObject;
        Require(Mathf.Approximately(archer.transform.localScale.x, 1.5f),
            "Archer must be half of its former scale 3.");
        for (int i = 1; i <= 4; i++)
        {
            Require(AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Enemy/Arrows/arrow_{i}.png") != null,
                $"Arrow animation frame {i} is missing.");
            Require(Resources.Load<Sprite>($"Enemy/Arrows/arrow_{i}") != null,
                $"Arrow animation frame {i} is not loadable at runtime.");
        }
        string[] skillIcons = { "IconDash", "IconSwordWave", "IconSpinSlash", "IconAshenJudgment" };
        foreach (string icon in skillIcons)
        {
            Require(AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/UI/Skills/{icon}.png") != null,
                $"Skill HUD icon {icon} is missing.");
            Require(Resources.Load<Sprite>($"UI/Skills/{icon}") != null,
                $"Skill HUD icon {icon} is not loadable at runtime.");
        }
        ValidateSkillHudLayering();

        foreach (string field in new[] { "batPrefab", "darkMagePrefab", "eliteEnemyPrefab" })
        {
            GameObject special = spawner.FindProperty(field).objectReferenceValue as GameObject;
            Require(special != null && Mathf.Approximately(special.transform.localScale.x, 4.5f) &&
                    Mathf.Approximately(special.transform.localScale.y, 4.5f),
                $"{field} should be 1.5 times its former scale 3.");
        }
        Texture2D impact = Resources.Load<Texture2D>("VFX/Combat/Explosions");
        Texture2D fire = Resources.Load<Texture2D>("VFX/Combat/Fire");
        Require(impact != null && impact.width == 1728 && impact.height == 192,
            "The shared hit animation sheet is missing or malformed.");
        Require(fire != null && fire.width == 896 && fire.height == 128,
            "The fire or boss hit animation sheet is missing or malformed.");
        foreach (string loaderName in new[] { "LoadExplosion", "LoadFire" })
        {
            MethodInfo loader = typeof(CombatHitVfx).GetMethod(loaderName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Sprite[] frames = loader?.Invoke(null, null) as Sprite[];
            Require(frames != null && frames.Length >= 7 && frames[0] != null,
                $"Combat impact frames did not load: {loaderName}.");
        }

        GameObject slime = spawner.FindProperty("poisonSlimePrefab").objectReferenceValue as GameObject;
        string slimeArt = AssetDatabase.GetAssetPath(slime.GetComponent<SpriteRenderer>().sprite);
        Require(slimeArt.EndsWith("PoisonSlimeReadable.png"), "Slime prefab still uses its old sprite.");
        EnemyConfigSO slimeStats = slime.GetComponent<EnemyHealth>().Config;
        Require(slimeStats.poisonDuration <= 2.5f && slimeStats.poisonTickInterval >= 1.75f &&
                slimeStats.attackCooldown >= 4.5f && slimeStats.specialCooldown >= 8.5f,
            "Poison damage-over-time was not reduced.");

        Texture2D potion = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/Pickups/PotionSet16x16.png");
        Require(potion != null && potion.width >= 64 && potion.height >= 16,
            "Green potion art must contain the bottom-right 16x16 tile.");
        Debug.Log("HEALTH_LEVEL_VALIDATION_OK: hearts, ten levels, room quotas, enemies, arrow animation, slime and four skill icons.");
    }

    private static void RequireEnemyPrefab(SerializedObject owner, string propertyName)
    {
        SerializedProperty property = owner.FindProperty(propertyName);
        GameObject prefab = property?.objectReferenceValue as GameObject;
        Require(prefab != null && prefab.GetComponent<EnemyHealth>() != null,
            $"Missing EnemyHealth prefab in generator field {propertyName}.");
        Require(prefab.GetComponent<EnemyHealth>().Config != null,
            $"Missing stat config in {propertyName}.");
        SpriteRenderer visual = prefab.GetComponentInChildren<SpriteRenderer>();
        Require(visual != null && visual.sprite != null,
            $"Missing recognizable sprite in {propertyName}.");
    }

    private static void ValidateSkillHudLayering()
    {
        GameObject testPlayer = new("Skill HUD Validation Player");
        GameObject canvas = null;
        try
        {
            SkillHudController hud = testPlayer.AddComponent<SkillHudController>();
            MethodInfo build = typeof(SkillHudController).GetMethod("CreateHud",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(build != null, "Skill HUD builder is missing.");
            build.Invoke(hud, null);
            canvas = GameObject.Find("Combat Skill HUD");
            Require(canvas != null, "Skill HUD canvas was not created.");

            foreach (string key in new[] { "SHIFT", "Q", "E", "R" })
            {
                Transform slot = canvas.transform.Find($"Skill Slots/Skill Slot {key}");
                Require(slot != null, $"Skill slot {key} was not created.");
                Transform icon = slot.Find("Skill Icon");
                Require(icon != null, $"Skill icon {key} was not created.");
                Image image = icon.GetComponent<Image>();
                Require(image != null && image.enabled && image.sprite != null,
                    $"Skill icon {key} is not visible or has no sprite.");
                Require(icon.GetSiblingIndex() > slot.Find("Obsidian Backplate").GetSiblingIndex() &&
                    icon.GetSiblingIndex() > slot.Find("Cooldown Fill").GetSiblingIndex(),
                    $"Skill icon {key} is behind a full-size overlay.");
                Require(slot.Find("Obsidian Cyan Frame") == null,
                    $"Skill slot {key} still has the opaque overlay that covered its icon.");
            }
        }
        finally
        {
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
            UnityEngine.Object.DestroyImmediate(testPlayer);
        }
    }

    private static void CheckGuaranteedEnemy(InfiniteWorldGenerator generator, SerializedObject owner,
        int levelNumber, string prefabField)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(InfiniteWorldGenerator).GetField("currentLevel", privateInstance)?.SetValue(generator, levelNumber);
        typeof(InfiniteWorldGenerator).GetField("random", privateInstance)?.SetValue(generator,
            new SeededRandom(9000 + levelNumber));
        MethodInfo choose = typeof(InfiniteWorldGenerator).GetMethod("ChooseEnemyPrefab", privateInstance);
        GameObject chosen = choose?.Invoke(generator, new object[] { 0, 0, true }) as GameObject;
        GameObject expected = owner.FindProperty(prefabField).objectReferenceValue as GameObject;
        Require(chosen == expected, $"Level {levelNumber} does not guarantee {prefabField}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
