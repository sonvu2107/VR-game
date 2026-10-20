using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Chỉ tự chạy khi có yêu cầu cụ thể trong Temp; không ghi scene đang có chỉnh sửa chưa lưu.
[InitializeOnLoad]
public static class CampaignMapsBuilder
{
    public const string ArtRoot = "Assets/Art/MapThemes/Campaign/";
    public const string SetPath = ArtRoot + "CampaignMapSet.asset";
    private const string Request = "Temp/CampaignMaps.install";
    private const string RebuildRequest = "Temp/CampaignMaps.rebuild";
    private static double nextCheck;
    private static readonly Vector2Int[] Neighbours = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    public static readonly string[] Ids = { "MossCrypt", "SunkenPrison", "EmberForge", "FrozenVault",
        "BoneCatacombs", "ForbiddenLibrary", "AbyssSanctum", "CrimsonThrone" };
    public static readonly string[] Names = { "Hầm Rêu Cổ", "Ngục Ngập Nước", "Lò Rèn Tro Tàn", "Kho Băng Cổ",
        "Hầm Mộ Xương", "Thư Viện Cấm", "Thánh Điện Vực Sâu", "Điện Huyết Vương" };
    private static readonly Color[] Flames = { new Color(.6f,1f,.25f), new Color(.1f,.85f,.9f),
        new Color(1f,.35f,.06f), new Color(.45f,.8f,1f), new Color(.7f,.4f,1f),
        new Color(1f,.7f,.25f), new Color(.7f,.2f,1f), new Color(1f,.15f,.15f) };

    static CampaignMapsBuilder() { EditorApplication.update += CheckRequest; }
    private static void CheckRequest()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if ((!File.Exists(Request) && !File.Exists(RebuildRequest)) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)) return;
        // Tiêu thụ yêu cầu trước khi mở scene/import để tránh chạy lồng nhau.
        bool rebuild=File.Exists(RebuildRequest);
        string request=rebuild?RebuildRequest:Request;
        File.Move(request, request + ".started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        try
        {
            if(rebuild) RebuildGeneratedPreviews(); else Install();
            ValidateLayouts();
            ValidateGeneratedCampaign();
            ExportStructureDiagram();
            File.WriteAllText("Temp/CampaignMaps.result.txt", "PASS: eight atlases, eight preview scenes, ten campaign maps; layout and generation validation complete.");
            if(rebuild) File.WriteAllText("Temp/CampaignMaps.play","Validate refreshed structural layouts through all ten levels.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/CampaignMaps.result.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Refresh Owned Preview Geometry")]
    public static void RebuildGeneratedPreviews()
    {
        Guard();
        string previous=EditorSceneManager.GetActiveScene().path;
        string backup="Temp/CampaignMapStructureBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        Directory.CreateDirectory(backup);
        try
        {
            for(int i=0;i<Ids.Length;i++)
            {
                string path=PreviewScene(i+3,Ids[i]), folder=ArtRoot+Ids[i]+"/";
                if(!File.Exists(path)) throw new FileNotFoundException("Build the original map preview first.",path);
                File.Copy(path,backup+"/"+Path.GetFileName(path));
                foreach(string meshPath in Directory.GetFiles(folder+"PreviewMeshes","*.asset"))
                    File.Copy(meshPath,backup+"/"+Ids[i]+"_"+Path.GetFileName(meshPath));
                EditorSceneManager.OpenScene(path);
                var generator=UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
                var theme=AssetDatabase.LoadAssetAtPath<CourtyardTheme>(folder+"Theme.asset");
                if(generator.courtyardTheme!=theme) throw new InvalidOperationException("Preview theme was edited; preserve it and review manually: "+path);
                foreach(var old in UnityEngine.Object.FindObjectsOfType<CourtyardPreviewOnly>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var layout=CampaignDungeonLayout.Generate(Sequence(7),new SeededRandom(12345),theme.layoutStyle);
                var root=ArcaneCitadelGeometry.Build(layout,theme,new HashSet<Vector2Int>());
                root.tag="EditorOnly"; root.AddComponent<CourtyardPreviewOnly>();
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    string meshPath=folder+"PreviewMeshes/"+filter.name.Replace(" ","")+".asset";
                    var persistent=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(persistent==null) AssetDatabase.CreateAsset(filter.sharedMesh,meshPath);
                    else
                    {
                        Mesh transient=filter.sharedMesh; EditorUtility.CopySerialized(transient,persistent);
                        filter.sharedMesh=persistent; EditorUtility.SetDirty(persistent);
                        AssetDatabase.SaveAssetIfDirty(persistent); UnityEngine.Object.DestroyImmediate(transient);
                    }
                }
                generator.playerTransform.position=PrefabPlacer.GetWorldPosition(layout.Rooms[0].SpawnPoint);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            LanBuildTools.RefreshCatalog();
            Debug.Log("CAMPAIGN_STRUCTURE_REFRESH_PASS previews=8 shapes=ellipse,clippedCourt,workshop,diamond,cross,hall,octagon,throne graphs=branches,bridges,wings,zigzag,deadEnds,hub,ring,axis");
        }
        finally { if(!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous); }
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Export Actual Floor Plans")]
    public static void ExportStructureDiagram()
    {
        const int width=1600,height=960,panelW=400,panelH=480;
        var pixels=Enumerable.Repeat(new Color32(15,19,29,255),width*height).ToArray();
        string[] digits={"111101101101111","010110010010111","111001111100111","111001111001111","101101111001001",
            "111100111001111","111100111101111","111001001001001","111101111101111","111101111001111"};
        void Fill(int x,int y,int w,int h,Color32 color)
        {
            for(int py=Math.Max(0,y);py<Math.Min(height,y+h);py++)
            for(int px=Math.Max(0,x);px<Math.Min(width,x+w);px++) pixels[py*width+px]=color;
        }
        for(int i=0;i<Ids.Length;i++)
        {
            int ox=(i%4)*panelW,oy=height-(i/4+1)*panelH;
            Fill(ox+2,oy+2,panelW-4,panelH-4,new Color32(24,30,43,255));
            string label=(i+3).ToString("00");
            for(int ch=0;ch<label.Length;ch++) for(int p=0;p<15;p++)
                if(digits[label[ch]-'0'][p]=='1') Fill(ox+18+ch*20+(p%3)*5,oy+panelH-38+(4-p/3)*5,5,5,new Color32(238,239,244,255));
            var layout=CampaignDungeonLayout.Generate(Sequence(7),new SeededRandom(12345),(CampaignLayoutStyle)i);
            int minX=layout.FloorTiles.Min(p=>p.x),maxX=layout.FloorTiles.Max(p=>p.x);
            int minY=layout.FloorTiles.Min(p=>p.y),maxY=layout.FloorTiles.Max(p=>p.y);
            float scale=Math.Min((panelW-36f)/(maxX-minX+3),(panelH-70f)/(maxY-minY+3));
            float dx=ox+(panelW-(maxX-minX+1)*scale)*.5f,dy=oy+18;
            void Cell(Vector2Int p,Color32 color)=>Fill(Mathf.FloorToInt(dx+(p.x-minX)*scale),Mathf.FloorToInt(dy+(p.y-minY)*scale),Mathf.CeilToInt(scale),Mathf.CeilToInt(scale),color);
            foreach(var tile in layout.FloorTiles) Cell(tile,new Color32(64,80,103,255));
            foreach(var room in layout.Rooms)
            {
                Color32 color=room.Type==RoomType.Start?new Color32(74,195,126,255):room.Type==RoomType.Treasure?new Color32(231,185,64,255):
                    room.Type==RoomType.Boss?new Color32(218,90,100,255):room.Type==RoomType.Exit?new Color32(83,195,223,255):
                    (Color32)Color.Lerp(new Color(.35f,.4f,.5f),Flames[i],.4f);
                foreach(var tile in room.FloorTiles) Cell(tile,color);
            }
        }
        var image=new Texture2D(width,height,TextureFormat.RGBA32,false); image.SetPixels32(pixels); image.Apply();
        Directory.CreateDirectory("Captures"); File.WriteAllBytes("Captures/CampaignMapStructures.png",image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        Debug.Log("CAMPAIGN_FLOORPLANS_EXPORTED Captures/CampaignMapStructures.png seed=12345 maps=8 actualFloorTiles=true");
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Build Levels 3-10")]
    public static void Install()
    {
        Guard();
        string previousScene = EditorSceneManager.GetActiveScene().path;
        string backup = "Temp/CampaignMapInstallBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        Directory.CreateDirectory(backup);
        File.Copy("Assets/Scenes/Dungeon.unity", backup + "/Dungeon.unity");
        var themes = new CourtyardTheme[10];
        themes[1] = AssetDatabase.LoadAssetAtPath<CourtyardTheme>("Assets/Art/MapThemes/ArcaneCitadel/ArcaneCitadelTheme.asset");
        if (themes[1] == null) throw new InvalidOperationException("Original Arcane theme missing.");
        for (int i = 0; i < Ids.Length; i++)
        {
            int level = i + 3;
            string folder = ArtRoot + Ids[i] + "/";
            EnsureFolder(folder.TrimEnd('/'));
            string image = folder + Ids[i] + "_Atlas_v1.png";
            if (!File.Exists(image)) throw new FileNotFoundException("Generate all campaign atlases first.", image);
            AssetDatabase.ImportAsset(image, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(image);
            importer.textureType = TextureImporterType.Default;
            // These atlases are minified heavily at the gameplay camera scale.
            // Point sampling without mipmaps creates temporal aliasing while the
            // camera scrolls, which reads as movement jitter on themed maps.
            importer.filterMode = FilterMode.Bilinear; importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
            importer.isReadable = true; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(image);
            Rect[] rects = ExtractRects(texture);
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "MapAtlas.mat");
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Sprites/Default shader missing.");
                material = new Material(shader) { name = Ids[i] + " Atlas" };
                AssetDatabase.CreateAsset(material, folder + "MapAtlas.mat");
            }
            material.mainTexture = texture;
            material.DisableKeyword("PIXELSNAP_ON");
            if (material.HasProperty("PixelSnap")) material.SetFloat("PixelSnap", 0f);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            var torch = AssetDatabase.LoadAssetAtPath<Material>(folder + "Torch.mat");
            if (torch == null)
            {
                var source = themes[1].arcaneTorch;
                if (source == null) throw new InvalidOperationException("Existing animated torch palette material missing.");
                torch = new Material(source) { name = Ids[i] + " Torch" };
                AssetDatabase.CreateAsset(torch, folder + "Torch.mat");
            }
            torch.SetColor("_FlameColor", Flames[i]); EditorUtility.SetDirty(torch); AssetDatabase.SaveAssetIfDirty(torch);
            string themePath = folder + "Theme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(themePath);
            if (theme == null) { theme = ScriptableObject.CreateInstance<CourtyardTheme>(); AssetDatabase.CreateAsset(theme, themePath); }
            // Giữ lựa chọn low-noise sau khi rebuild/import atlas, không bật lại nền nhiễu.
            var lowNoise = AssetDatabase.LoadAssetAtPath<Material>(folder + "LowNoiseFloorWalls.mat");
            if (lowNoise != null)
            {
                lowNoise.mainTexture = texture; EditorUtility.SetDirty(lowNoise); AssetDatabase.SaveAssetIfDirty(lowNoise);
            }
            theme.arcaneFloor = theme.arcaneWalls = lowNoise != null ? lowNoise : material;
            theme.arcaneProps = theme.arcaneSpike = material;
            theme.arcaneTorch = torch; theme.usesCampaignAtlas = true;
            theme.layoutStyle = (CampaignLayoutStyle)i; theme.mapDisplayName = Names[i];
            theme.torchLightColor = Flames[i]; theme.atlasUvs = rects;
            theme.arcaneTrapsPerCombatRoom = level < 5 ? 2 : level < 8 ? 3 : 4;
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
            themes[level - 1] = theme;
            // Pixel reads chỉ dùng để đo UV trong Editor, không giữ bản sao CPU ở runtime.
            importer.isReadable = false; importer.SaveAndReimport();
            string scenePath = PreviewScene(level, Ids[i]);
            if (!File.Exists(scenePath)) CreatePreview(scenePath, theme, folder);
        }
        var set = AssetDatabase.LoadAssetAtPath<CampaignMapSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<CampaignMapSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        set.maps = themes; EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        var serialized = new SerializedObject(generator);
        serialized.FindProperty("campaignMaps").objectReferenceValue = set;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        var scenes = EditorBuildSettings.scenes.ToList();
        for (int i = 0; i < Ids.Length; i++)
        {
            string path = PreviewScene(i + 3, Ids[i]);
            if (!scenes.Any(scene => scene.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        LanBuildTools.RefreshCatalog();
        if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene)) EditorSceneManager.OpenScene(previousScene);
        Debug.Log("CAMPAIGN_MAPS_INSTALLED originalLv1=true arcaneLv2=true newMaps=8 newAtlases=8 LANcatalog=true");
    }

    private static void CreatePreview(string path, CourtyardTheme theme, string folder)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        var data = new SerializedObject(generator);
        data.FindProperty("campaignMaps").objectReferenceValue = null;
        data.FindProperty("campaignSecondMap").objectReferenceValue = null;
        data.FindProperty("courtyardTheme").objectReferenceValue = theme;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (var old in UnityEngine.Object.FindObjectsOfType<CourtyardPreviewOnly>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var layout = CampaignDungeonLayout.Generate(Sequence(7), new SeededRandom(12345), theme.layoutStyle);
        generator.tilemapVisualizer.Clear(); generator.tilemapVisualizer.SetMapArtVisible(false);
        var root = ArcaneCitadelGeometry.Build(layout, theme, new HashSet<Vector2Int>());
        root.tag = "EditorOnly"; root.AddComponent<CourtyardPreviewOnly>();
        EnsureFolder(folder + "PreviewMeshes");
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            AssetDatabase.CreateAsset(filter.sharedMesh, folder + "PreviewMeshes/" + filter.name.Replace(" ", "") + ".asset");
        generator.playerTransform.position = PrefabPlacer.GetWorldPosition(layout.Rooms[0].SpawnPoint);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    private static Rect[] ExtractRects(Texture2D image)
    {
        Color32[] pixels = image.GetPixels32();
        var result = new Rect[16];
        for (int cell = 0; cell < 16; cell++)
        {
            int x0 = Mathf.RoundToInt(cell % 4 * image.width / 4f) + 3;
            int x1 = Mathf.RoundToInt((cell % 4 + 1) * image.width / 4f) - 3;
            int y0 = Mathf.RoundToInt((3 - cell / 4) * image.height / 4f) + 3;
            int y1 = Mathf.RoundToInt((4 - cell / 4) * image.height / 4f) - 3;
            // Trap frames keep one fixed canvas; props/trim use largest alpha component to remove stray grid pixels.
            if (cell >= 5 && cell <= 11)
            {
                var visited = new HashSet<int>();
                int largest = 0; var bounds = new RectInt(x0,y0,x1-x0,y1-y0);
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                {
                    int key = y * image.width + x;
                    if (pixels[key].a < 128 || !visited.Add(key)) continue;
                    var queue = new Queue<int>(); queue.Enqueue(key);
                    int minX=x,maxX=x,minY=y,maxY=y,count=0;
                    while (queue.Count > 0)
                    {
                        int point=queue.Dequeue(), px=point%image.width, py=point/image.width; count++;
                        minX=Math.Min(minX,px); maxX=Math.Max(maxX,px); minY=Math.Min(minY,py); maxY=Math.Max(maxY,py);
                        foreach (Vector2Int d in Neighbours)
                        {
                            int nx=px+d.x,ny=py+d.y,n=ny*image.width+nx;
                            if (nx>=x0 && nx<x1 && ny>=y0 && ny<y1 && pixels[n].a>=128 && visited.Add(n)) queue.Enqueue(n);
                        }
                    }
                    if (count>largest) { largest=count; bounds=new RectInt(minX,minY,maxX-minX+1,maxY-minY+1); }
                }
                if(largest<32) throw new InvalidOperationException("Empty atlas cell: " + cell);
                x0=bounds.xMin; x1=bounds.xMax; y0=bounds.yMin; y1=bounds.yMax;
            }
            result[cell] = new Rect(x0/(float)image.width,y0/(float)image.height,(x1-x0)/(float)image.width,(y1-y0)/(float)image.height);
        }
        return result;
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Validate Layouts")]
    public static void ValidateLayouts()
    {
        int cases=0;
        var signatures=new HashSet<string>();
        for(int style=0;style<8;style++)
        for(int count=6;count<=7;count++)
        for(int seed=0;seed<24;seed++)
        {
            var layout=CampaignDungeonLayout.Generate(Sequence(count),new SeededRandom(seed),(CampaignLayoutStyle)style);
            if(!DungeonValidator.IsValid(layout,out string reason)) throw new InvalidOperationException($"Map {style}, seed {seed}: {reason}");
            var repeat=CampaignDungeonLayout.Generate(Sequence(count),new SeededRandom(seed),(CampaignLayoutStyle)style);
            if(!layout.FloorTiles.SetEquals(repeat.FloorTiles)) throw new InvalidOperationException("Map is not deterministic.");
            if(count==7 && seed==0 && !signatures.Add(string.Join(";",layout.FloorTiles.OrderBy(p=>p.x).ThenBy(p=>p.y))))
                throw new InvalidOperationException("Two campaign maps share a layout.");
            cases++;
        }
        Debug.Log($"CAMPAIGN_LAYOUTS_PASS cases={cases} uniqueLayouts={signatures.Count} connected=true noOverlap=true deterministic=true");
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Validate Generated Campaign")]
    public static void ValidateGeneratedCampaign()
    {
        Guard();
        int failClosed=0;
        try
        {
            var campaign=LevelDefinition.CreateDefaultCampaign();
            for(int level=1;level<=10;level++)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
                var generator=UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
                if(!generator.GenerateLevel(campaign[level-1],10,12345+level*1009)) throw new InvalidOperationException("Cannot generate level "+level);
                var theme=generator.ActiveTheme;
                if(level==1 && theme!=null || level==2 && (theme==null || theme.usesCampaignAtlas) ||
                    level>=3 && (theme==null || !theme.usesCampaignAtlas || (int)theme.layoutStyle!=level-3))
                    throw new InvalidOperationException("Wrong campaign theme at level "+level);
                TacticalTrapValidation.ValidateCurrent(generator);
                if (level == 10)
                {
                    var candidates = generator.GetFinalBossCandidates();
                    if (candidates.Count != 1 || candidates[0].GetComponent<SkeletonKingController>() == null)
                        throw new InvalidOperationException("Final boss must be Skeleton King.");
                    var finalBoss = UnityEngine.Object.FindObjectOfType<CampaignFinalBoss>();
                    if (finalBoss == null || finalBoss.SizeMultiplier > 1.75f)
                        throw new InvalidOperationException("Final boss scale exceeds new map budget.");
                    var names = UnityEngine.Object.FindObjectsOfType<Transform>();
                    if (names.Count(t => t.name == "Royal Throne Footprint") != 1 ||
                        names.Count(t => t.name == "Royal Honor Statue Footprint") != 2 ||
                        names.Count(t => t.name == "Throne General") != 2 || names.Count(t => t.name == "Throne Guard") != 4)
                        throw new InvalidOperationException("Royal court formation missing or duplicated.");
                    var royalGeometry = UnityEngine.Object.FindObjectOfType<ArcaneCitadelGeometry>();
                    var floor = royalGeometry.GetComponentsInChildren<MeshFilter>().First(f => f.name == "Arcane Floor").sharedMesh;
                    Vector2 emblem = new Vector2(theme.AtlasUv(3).xMin, theme.AtlasUv(3).yMin);
                    if (floor.uv.Where((uv, index) => index % 4 == 0 && Vector2.Distance(uv, emblem) < .0001f).Count() != 1)
                        throw new InvalidOperationException("Royal emblem must appear exactly once across the entire map.");
                }
                if(!generator.LastTrapPlan.RoutesVerified) failClosed++;
                if(level>=3 && UnityEngine.Object.FindObjectsOfType<ArcaneCitadelGeometry>().Length!=1) throw new InvalidOperationException("Map geometry duplicated/missing.");
                ValidatePlayerBoundary(generator, level);
            }
            Debug.Log($"CAMPAIGN_GENERATION_PASS levels=10 uniqueThemes=10 trapsValidated=true playerBoundary=true failClosed={failClosed}");
        }
        finally { EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity"); }
    }

    private static void ValidatePlayerBoundary(InfiniteWorldGenerator generator, int level)
    {
        if (generator.GeneratedFloorTiles == null || generator.GeneratedRooms == null)
            throw new InvalidOperationException("Missing generated floor data at level " + level);

        var floor = new HashSet<Vector2Int>(generator.GeneratedFloorTiles);
        PlayerMovement movement = generator.playerTransform != null
            ? generator.playerTransform.GetComponent<PlayerMovement>()
            : null;
        if (movement == null || !movement.HasWalkableAreaConstraint)
            throw new InvalidOperationException("Player boundary is not configured at level " + level);

        Vector2 lookupOffset = new(.5f, -.5f);
        Vector2[] directions =
        {
            Vector2.left, Vector2.right, Vector2.up, Vector2.down,
            new Vector2(1,1).normalized, new Vector2(-1,1).normalized,
            new Vector2(1,-1).normalized, new Vector2(-1,-1).normalized
        };

        foreach (DungeonRoom room in generator.GeneratedRooms)
        foreach (Vector2 direction in directions)
        {
            Vector2 start = PrefabPlacer.GetWorldPosition(room.SpawnPoint);
            if (!WalkableAreaConstraint.ContainsFootprint(
                floor, start, Vector2.zero, Vector2.zero, cellLookupOffset: lookupOffset))
                throw new InvalidOperationException($"Invalid player footprint at level {level}, room {room.Type}.");

            Vector2 result = start + WalkableAreaConstraint.ConstrainDelta(
                floor, start, direction * 100f, Vector2.zero, Vector2.zero, lookupOffset);
            if (!WalkableAreaConstraint.ContainsFootprint(
                floor, result, Vector2.zero, Vector2.zero, cellLookupOffset: lookupOffset))
                throw new InvalidOperationException($"Boundary escape at level {level}, room {room.Type}, direction {direction}.");
        }
    }

    private static List<RoomType> Sequence(int count)
    {
        var result=new List<RoomType>{RoomType.Start};
        for(int i=0;i<count-4;i++) result.Add(RoomType.Combat);
        result.Add(RoomType.Treasure); result.Add(RoomType.Boss); result.Add(RoomType.Exit); return result;
    }
    private static string PreviewScene(int level,string id) => $"Assets/Scenes/Level{level:00}_{id}.unity";
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        int slash=path.LastIndexOf('/'); EnsureFolder(path.Substring(0,slash)); AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
    }
    private static void Guard()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play trước khi dựng map.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Lưu scene đang chỉnh trước khi dựng map.");
    }
}
