using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class ExpandedEnemyIntegration
{
    public const string ArtFolder = "Assets/Scripts/ScriptableObjects/EnemyArt";
    public static readonly string[] Names = { "GoblinTorch", "GoblinBarrel", "GoblinTNT" };
    public static readonly int[] FirstLevels = { 1, 1, 1 };
    public static readonly int[] LastLevels = { 10, 10, 10 };
    public static float VisualScale(string name) => name == "GoblinTorch" ? 0.7f : 0.75f;

    // Chỉ sửa roster và phần hình/collider của Goblin, không ghi lại scene hay cân bằng chiến đấu.
    public static void ApplyRosterAndScale()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        var roster = AssetDatabase.LoadAssetAtPath<EnemyRosterSO>(ArtFolder + "/ExpandedRoster.asset");
        if (roster == null) throw new InvalidOperationException("Missing expanded roster.");
        var entries = new EnemyRosterSO.Entry[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            string path = SkullVampireIntegration.PrefabFolder + Names[i] + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.GetComponent<EnemySheetAnimation>().Renderer.transform.localScale = Vector3.one * VisualScale(Names[i]);
                root.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 0.5f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            entries[i] = new EnemyRosterSO.Entry { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                firstLevel = FirstLevels[i], lastLevel = LastLevels[i], introduceInWideRoom = Names[i] == "GoblinTNT" };
        }
        roster.entries = entries;
        Save(roster);
        LanBuildTools.RefreshCatalog();
        Validate();
    }

    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before installing mobs.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save unsaved scene edits before installing mobs.");
        EnsureFolder(ArtFolder);
        EnsureFolder("Assets/Prefabs/EnemyBoss/Projectiles");
        EnemySheetArtSO dynamite = CreateArt("Dynamite", 64, 100f,
            new[] { "Assets/Imports/Tiny Swords (Update 010)/Factions/Goblins/Troops/TNT/Dynamite/Dynamite.png" });
        dynamite.idle = dynamite.move = dynamite.attack = new EnemySheetArtSO.Clip(0, 0, 6);
        Save(dynamite);
        GameObject projectile = CreateDynamite(dynamite);
        var entries = new EnemyRosterSO.Entry[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            string name = Names[i];
            var config = CreateConfig(name);
            if (name == "GoblinTNT") { config.projectilePrefab = projectile; Save(config); }
            EnemySheetArtSO art = null;
            if (name.StartsWith("Goblin", StringComparison.Ordinal))
            {
                string kind = name.Substring(6);
                string[] paths = new[] { "Blue", "Red", "Purple", "Yellow" }.Select(color =>
                    $"Assets/Imports/Tiny Swords (Update 010)/Factions/Goblins/Troops/{kind}/{color}/{kind}_{color}.png").ToArray();
                art = CreateArt(name, kind == "Barrel" ? 128 : 192, 64f, paths);
                art.idle = new EnemySheetArtSO.Clip(0, 0, kind == "Torch" ? 7 : kind == "TNT" ? 6 : 1);
                art.move = new EnemySheetArtSO.Clip(1, 0, 6);
                art.attack = new EnemySheetArtSO.Clip(kind == "Barrel" ? 4 : 2, 0, kind == "Barrel" ? 3 : kind == "TNT" ? 7 : 6);
                if (kind == "Torch")
                {
                    art.attackDown = new EnemySheetArtSO.Clip(3, 0, 6);
                    art.attackUp = new EnemySheetArtSO.Clip(4, 0, 6);
                }
                if (kind == "Barrel")
                {
                    art.reverseIdle = new EnemySheetArtSO.Clip(2, 0, 1);
                    art.reverseMove = new EnemySheetArtSO.Clip(3, 0, 6);
                    art.attackUp = new EnemySheetArtSO.Clip(5, 0, 3);
                }
                Save(art);
            }
            GameObject prefab = CreateMob(name, config, art);
            entries[i] = new EnemyRosterSO.Entry { prefab = prefab, firstLevel = FirstLevels[i],
                lastLevel = LastLevels[i], introduceInWideRoom = name == "GoblinTNT" };
        }
        AddAlternate("Skull", "skull", "skull");
        AddAlternate("Vampire", "vampire", "vampire");
        string rosterPath = ArtFolder + "/ExpandedRoster.asset";
        var roster = AssetDatabase.LoadAssetAtPath<EnemyRosterSO>(rosterPath);
        if (roster == null) { roster = ScriptableObject.CreateInstance<EnemyRosterSO>(); AssetDatabase.CreateAsset(roster, rosterPath); }
        roster.entries = entries;
        Save(roster);
        foreach (string path in SkullVampireIntegration.Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var generator in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InfiniteWorldGenerator>(true)))
            {
                var data = new SerializedObject(generator);
                data.FindProperty("additionalEnemyRoster").objectReferenceValue = roster;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }
        LanBuildTools.RefreshCatalog();
        Validate();
        Debug.Log("EXPANDED_ENEMIES_INSTALL_PASS types=3 goblinColors=12");
    }

    private static EnemyConfigSO CreateConfig(string name)
    {
        string path = "Assets/Scripts/ScriptableObjects/Enemies/" + name + ".asset";
        var config = AssetDatabase.LoadAssetAtPath<EnemyConfigSO>(path);
        if (config == null) { config = ScriptableObject.CreateInstance<EnemyConfigSO>(); AssetDatabase.CreateAsset(config, path); }
        config.displayName = name;
        config.archetype = name == "GoblinTNT" ? EnemyArchetype.Ranged : name == "GoblinBarrel" ? EnemyArchetype.Charger : EnemyArchetype.Melee;
        config.maxHealth = name == "GoblinBarrel" ? 55 : 70;
        config.chaseSpeed = 2.1f;
        config.wanderSpeed = 0.9f;
        config.detectionRange = name == "GoblinTNT" ? 8f : 7f;
        config.attackRange = name == "GoblinTNT" ? 5f : 1.2f;
        config.preferredRange = config.attackRange;
        config.contactDamage = name == "GoblinBarrel" || name == "GoblinTNT" ? 0 : 1;
        config.attackDamage = 1;
        config.attackCooldown = name == "GoblinTNT" ? 3.5f : 2.4f;
        config.attackWindup = name == "GoblinTNT" ? 0.7f : 0.55f;
        config.attackRecovery = 0.55f;
        config.specialCooldown = 4f;
        config.specialRange = 4f;
        config.dashSpeed = 4.5f;
        config.dashDuration = 0.6f;
        config.blastRadius = name == "GoblinBarrel" ? 1.8f : name == "GoblinTNT" ? 1.4f : 0f;
        config.selfDestructOnAttack = name == "GoblinBarrel";
        config.projectileSpeed = 3.5f;
        config.projectileLifetime = 2f;
        config.lifeStealHealthPerDamage = 0;
        config.scoreValue = 15;
        Save(config);
        return config;
    }

    private static EnemySheetArtSO CreateArt(string name, int cellSize, float ppu, string[] paths)
    {
        string path = ArtFolder + "/" + name + ".asset";
        var art = AssetDatabase.LoadAssetAtPath<EnemySheetArtSO>(path);
        if (art == null) { art = ScriptableObject.CreateInstance<EnemySheetArtSO>(); AssetDatabase.CreateAsset(art, path); }
        art.cellSize = cellSize;
        art.pixelsPerUnit = ppu;
        art.variants = paths.Select(LoadTexture).ToArray();
        return art;
    }
    private static Texture2D LoadTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing source art: " + path);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static Sprite[] LoadPixelFrames(string folder, string prefix, int version)
    {
        return Enumerable.Range(1, 4).Select(frame =>
        {
            string path = $"Assets/Imports/2D Pixel Dungeon Asset Pack/Character_animation/monsters_idle/{folder}/v{version}/{prefix}_v{version}_{frame}.png";
            LoadTexture(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }).ToArray();
    }
    private static GameObject CreateMob(string name, EnemyConfigSO config, EnemySheetArtSO art)
    {
        string path = SkullVampireIntegration.PrefabFolder + name + ".prefab";
        string template = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null ? path : SkullVampireIntegration.PrefabFolder + "Skull.prefab";
        var root = PrefabUtility.LoadPrefabContents(template);
        try
        {
            root.name = name;
            var renderer = root.GetComponent<EnemySpriteAnimation>() != null ? root.GetComponent<EnemySpriteAnimation>().Renderer :
                root.GetComponent<EnemySheetAnimation>().Renderer;
            if (art != null)
            {
                var old = root.GetComponent<EnemySpriteAnimation>();
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var animation = root.GetComponent<EnemySheetAnimation>();
                if (animation == null) animation = root.AddComponent<EnemySheetAnimation>();
                SetReference(animation, "art", art);
                SetReference(animation, "visual", renderer);
                renderer.sprite = null;
                renderer.transform.localScale = Vector3.one * VisualScale(name);
            }
            foreach (var component in root.GetComponents<MonoBehaviour>())
            {
                var data = new SerializedObject(component);
                var field = data.FindProperty("config");
                if (field != null && field.propertyType == SerializedPropertyType.ObjectReference)
                { field.objectReferenceValue = config; data.ApplyModifiedPropertiesWithoutUndo(); }
            }
            root.GetComponent<EnemyMovement>().SetVisualRenderer(renderer);
            root.GetComponent<NavMeshAgent>().speed = config.chaseSpeed;
            root.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 0.5f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
    private static GameObject CreateDynamite(EnemySheetArtSO art)
    {
        string path = "Assets/Prefabs/EnemyBoss/Projectiles/Dynamite.prefab";
        var root = new GameObject("DynamiteProjectile");
        try
        {
            root.layer = LayerMask.NameToLayer("Enemy");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 3;
            var animation = root.AddComponent<EnemySheetAnimation>();
            SetReference(animation, "art", art);
            SetReference(animation, "visual", renderer);
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.12f;
            collider.isTrigger = true;
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            root.AddComponent<EnemyProjectile>();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void AddAlternate(string name, string folder, string prefix)
    {
        string path = SkullVampireIntegration.PrefabFolder + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { SetFrames(root.GetComponent<EnemySpriteAnimation>(), "alternateFrames", LoadPixelFrames(folder, prefix, 2)); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetFrames(UnityEngine.Object target, string field, Sprite[] frames)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(field); property.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Save(UnityEngine.Object target) { EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target); }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int index = path.LastIndexOf('/'); string parent = path.Substring(0, index); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(index + 1));
    }
    public static void Validate()
    {
        var roster = AssetDatabase.LoadAssetAtPath<EnemyRosterSO>(ArtFolder + "/ExpandedRoster.asset");
        if (roster == null || roster.entries.Length != Names.Length || roster.entries.Any(entry =>
            entry == null || entry.prefab == null || !Names.Contains(entry.prefab.name)))
            throw new InvalidOperationException("Roster includes removed or missing mobs.");
        var catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog"); catalog.Initialize();
        foreach (string name in Names)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkullVampireIntegration.PrefabFolder + name + ".prefab");
            if (prefab == null || prefab.GetComponent<EnemyHealth>() == null || prefab.GetComponent<EnemyBrain>().Config.displayName != name)
                throw new InvalidOperationException("Incomplete enemy: " + name);
            var sheet = prefab.GetComponent<EnemySheetAnimation>();
            if (sheet != null)
            {
                if (Vector3.Distance(sheet.Renderer.transform.localScale, Vector3.one * VisualScale(name)) > 0.001f)
                    throw new InvalidOperationException("Wrong Goblin visual scale: " + name);
                foreach (var texture in sheet.Art.variants)
                    if (catalog.Id(texture) < 0) throw new InvalidOperationException("Missing LAN sheet: " + texture.name);
            }
        }
        Debug.Log("EXPANDED_ENEMIES_CONTENT_PASS types=3 goblinSheets=12 retiredSkeletons=false scaled=true");
    }
}
