using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Tạo nội dung bằng Unity API, không sửa YAML hoặc cắt lại sprite gốc.</summary>
public static class SkullVampireIntegration
{
    public const string PrefabFolder = "Assets/Prefabs/EnemyBoss/";
    public static readonly string[] Scenes =
    {
        "Assets/Scenes/Dungeon.unity",
        "Assets/Scenes/DungeonCourtyardPrototype.unity",
        "Assets/Scenes/DungeonArcaneCitadel.unity"
    };

    public static void InstallBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Dừng Play Mode trước khi cài nội dung.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Lưu scene đang chỉnh trước khi cài nội dung.");

        Debug.Log("SKULL_VAMPIRE_PIPELINE " + DetectPipeline());
        GameObject skull = CreateEnemy("Skull", "skull", "Bat", 1.05f, false);
        GameObject vampire = CreateEnemy("Vampire", "vampire", "EliteEnemy", 1.35f, true);
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (InfiniteWorldGenerator generator in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<InfiniteWorldGenerator>(true)))
            {
                var serialized = new SerializedObject(generator);
                serialized.FindProperty("skullPrefab").objectReferenceValue = skull;
                serialized.FindProperty("vampirePrefab").objectReferenceValue = vampire;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(generator);
            }
            EditorSceneManager.SaveScene(scene);
        }
        LanBuildTools.RefreshCatalog();
        ValidateContent();
        HealthAndLevelValidation.Validate();
        EnemyDistributionValidation.Validate();
        Debug.Log("SKULL_VAMPIRE_INSTALL_PASS");
        SkullVampirePlayValidation.Run();
    }

    private static string DetectPipeline()
    {
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (pipeline == null) return "BuiltIn";
        string name = pipeline.GetType().FullName ?? "";
        return name.Contains("Universal") ? "URP" : name.Contains("HighDefinition") ? "HDRP" : "Custom";
    }

    private static GameObject CreateEnemy(string name, string framePrefix, string templateName,
        float visualSize, bool charges)
    {
        Sprite[] frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            string path = $"Assets/Imports/2D Pixel Dungeon Asset Pack/Character_animation/monsters_idle/{framePrefix}/v1/{framePrefix}_v1_{i + 1}.png";
            // Chỉ chỉnh lấy mẫu/nén texture; không đổi SpriteRect, pivot, PPU hay fileID.
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing frame: " + path);
            if (importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (frames[i] == null) throw new InvalidOperationException("Missing sprite: " + path);
            if (frames[i].rect != frames[0].rect || frames[i].pivot != frames[0].pivot)
                throw new InvalidOperationException("Frame dimensions/pivots differ: " + path);
        }

        string configPath = $"Assets/Scripts/ScriptableObjects/Enemies/{name}.asset";
        var config = AssetDatabase.LoadAssetAtPath<EnemyConfigSO>(configPath);
        if (config == null)
        {
            config = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyConfigSO>(
                $"Assets/Scripts/ScriptableObjects/Enemies/{templateName}.asset"));
            config.name = name;
            AssetDatabase.CreateAsset(config, configPath);
        }
        config.displayName = name;
        config.archetype = charges ? EnemyArchetype.Charger : EnemyArchetype.Melee;
        config.maxHealth = charges ? 85 : 65;
        config.contactDamage = 1;
        config.attackDamage = 1;
        config.chaseSpeed = charges ? 1.85f : 3.2f;
        config.wanderSpeed = charges ? 0.85f : 1.0f;
        config.detectionRange = charges ? 8f : 7f;
        config.attackRange = charges ? 1.25f : 1.0f;
        config.preferredRange = config.attackRange;
        config.attackCooldown = charges ? 3f : 1.8f;
        config.lifeStealHealthPerDamage = charges ? 12 : 0;
        config.attackWindup = charges ? 0.55f : 0.4f;
        config.attackRecovery = 0.6f;
        config.specialRange = 3.2f;
        config.dashSpeed = 4.2f;
        config.dashDuration = 0.35f;
        config.summonPrefab = null;
        config.projectilePrefab = null;
        config.scoreValue = charges ? 20 : 10;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);

        string prefabPath = PrefabFolder + name + ".prefab";
        string sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
            ? prefabPath : PrefabFolder + templateName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            root.name = name;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            foreach (var oldVisual in root.GetComponents<EnemyLocomotionVisual>())
                UnityEngine.Object.DestroyImmediate(oldVisual);
            foreach (var oldVisual in root.GetComponents<Boss3DVisual>())
                UnityEngine.Object.DestroyImmediate(oldVisual);
            var animator = root.GetComponent<Animator>();
            if (animator != null) UnityEngine.Object.DestroyImmediate(animator);
            SpriteRenderer source = root.GetComponent<SpriteRenderer>();
            source.enabled = false;
            Transform art = root.transform.Find("Animated Sprite");
            if (art == null)
            {
                art = new GameObject("Animated Sprite").transform;
                art.SetParent(root.transform, false);
            }
            art.localPosition = Vector3.zero;
            // Chuẩn hóa bằng transform hiển thị, không scale root/collider hoặc sửa PPU gốc.
            float scale = visualSize / frames[0].bounds.size.y;
            art.localScale = new Vector3(scale, scale, 1f);
            var renderer = art.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = art.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = Mathf.Max(2, source.sortingOrder);
            renderer.color = Color.white;
            renderer.enabled = true;

            var animation = root.GetComponent<EnemySpriteAnimation>();
            if (animation == null) animation = root.AddComponent<EnemySpriteAnimation>();
            var animationData = new SerializedObject(animation);
            animationData.FindProperty("visual").objectReferenceValue = renderer;
            animationData.FindProperty("framesPerSecond").floatValue = 8f;
            SerializedProperty frameData = animationData.FindProperty("frames");
            frameData.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                frameData.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            animationData.ApplyModifiedPropertiesWithoutUndo();
            foreach (var component in root.GetComponents<MonoBehaviour>())
            {
                if (component == null) throw new InvalidOperationException("Missing prefab script.");
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.FindProperty("config");
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                    property.objectReferenceValue = config;
                if (component is EnemyMovement)
                    serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var collider = root.GetComponent<BoxCollider2D>();
            collider.offset = Vector2.zero;
            collider.size = charges ? new Vector2(0.65f, 0.9f) : new Vector2(0.65f, 0.65f);
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1f;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    public static void ValidateContent()
    {
        LanAssetCatalog catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog");
        catalog.Initialize();
        foreach (string name in new[] { "Skull", "Vampire" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            Require(prefab != null && prefab.GetComponent<EnemyBrain>()?.Config?.displayName == name,
                name + " prefab/config missing.");
            var visual = prefab.GetComponent<EnemySpriteAnimation>();
            Require(visual != null && visual.Renderer != null && visual.Renderer.enabled,
                name + " visual missing.");
            var data = new SerializedObject(visual);
            var frames = data.FindProperty("frames");
            Require(frames.arraySize == 4, name + " needs four frames.");
            for (int i = 0; i < frames.arraySize; i++)
                Require(catalog.Id(frames.GetArrayElementAtIndex(i).objectReferenceValue) >= 0,
                    name + " frame missing from LAN catalog.");
        }
        foreach (string path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
            var data = new SerializedObject(generator);
            Require(data.FindProperty("skullPrefab").objectReferenceValue != null &&
                data.FindProperty("vampirePrefab").objectReferenceValue != null, path + " missing references.");
            // Kiểm tra chính hàm chọn quái đang dùng, đồng thời giữ các mốc quái cũ.
            FieldInfo level = typeof(InfiniteWorldGenerator).GetField("currentLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo choose = typeof(InfiniteWorldGenerator).GetMethod("ChooseEnemyPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            string[] guaranteed = { "", "", "skullPrefab", "skeletonArcherPrefab", "poisonSlimePrefab", "", "batPrefab", "eliteEnemyPrefab", "darkMagePrefab" };
            for (int number = 2; number <= 8; number++)
            {
                if (guaranteed[number] == "") continue;
                level.SetValue(generator, number);
                Require((GameObject)choose.Invoke(generator, new object[] { 0, 0, true }) == data.FindProperty(guaranteed[number]).objectReferenceValue,
                    path + " introduction changed at level " + number);
            }
            level.SetValue(generator, 7);
            Require((GameObject)choose.Invoke(generator, new object[] { 1, 0, false }) == data.FindProperty("vampirePrefab").objectReferenceValue,
                "Vampire introduction missing.");
            Debug.Log("SKULL_VAMPIRE_SCENE_PASS " + path);
        }
        Debug.Log("SKULL_VAMPIRE_CONTENT_PASS prefabs=2 frames=8 scenes=3 LAN=8/8");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
