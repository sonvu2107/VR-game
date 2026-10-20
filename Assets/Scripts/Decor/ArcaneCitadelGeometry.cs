using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Arcane map geometry. Reads atlas UVs; static meshes are replicated by the LAN world packet.</summary>
public sealed class ArcaneCitadelGeometry : MonoBehaviour
{
    private readonly List<Mesh> ownedMeshes = new();
    private CourtyardTheme artTheme;
    private readonly List<Rect> decorImageBounds = new();
    private readonly List<Rect> fireImageBounds = new();

    public static GameObject Build(DungeonLayout layout, CourtyardTheme theme, HashSet<Vector2Int> reservedCells = null)
    {
        var root = new GameObject("Arcane Citadel - New Atlas Geometry");
        var owner = root.AddComponent<ArcaneCitadelGeometry>();
        owner.artTheme = theme;
        if (theme.usesCampaignAtlas) root.name = theme.mapDisplayName + " - Atlas Geometry";
        Rect Tile(int index) => theme.usesCampaignAtlas
            ? PixelSafeUv(theme.AtlasUv(index), theme.arcaneFloor != null ? theme.arcaneFloor.mainTexture : null)
            : CellUv(index);
        var floor = new Batch();
        var walls = new Batch();
        var props = new Batch();
        var border = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in layout.FloorTiles.OrderBy(c => c.y).ThenBy(c => c.x))
        {
            int variant = (int)((uint)(cell.x * 73856093 ^ cell.y * 19349663) % (theme.usesCampaignAtlas ? 3u : 4u));
            floor.Quad(new Rect(cell.x, cell.y, 1, 1), Tile(variant));
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int neighbour = cell + new Vector2Int(x, y);
                if (!layout.FloorTiles.Contains(neighbour)) border.Add(neighbour);
            }
        }
        foreach (Vector2Int cell in border.OrderBy(c => c.y).ThenBy(c => c.x))
        {
            // One masonry scale for BOTH directions and all junction cells.
            // Separate coping follows the outer contour, including L corners;
            // no mismatched horizontal facade / vertical pillar modules.
            walls.Quad(new Rect(cell.x, cell.y, 1, 1), theme.usesCampaignAtlas ? Tile(4) : FromTop(38, 133, 255, 110));
            bool topEdge = Outside(cell + Vector2Int.up, layout.FloorTiles, border);
            bool bottomEdge = Outside(cell + Vector2Int.down, layout.FloorTiles, border);
            bool leftEdge = Outside(cell + Vector2Int.left, layout.FloorTiles, border);
            bool rightEdge = Outside(cell + Vector2Int.right, layout.FloorTiles, border);
            Rect coping = theme.usesCampaignAtlas ? Tile(5) : FromTop(39, 87, 252, 27);
            const float rim = 0.18f;
            if (topEdge) walls.Quad(new Rect(cell.x, cell.y + 1 - rim, 1, rim), coping);
            if (bottomEdge) walls.Quad(new Rect(cell.x, cell.y, 1, rim), coping);
            if (leftEdge) walls.Quad(new Rect(cell.x, cell.y, rim, 1), coping, rotateUv: true);
            if (rightEdge) walls.Quad(new Rect(cell.x + 1 - rim, cell.y, rim, 1), coping, rotateUv: true);
            Rect corner = theme.usesCampaignAtlas ? Tile(6) : FromTop(92, 88, 26, 25);
            if (topEdge && leftEdge) walls.Quad(new Rect(cell.x, cell.y + 1 - rim, rim, rim), corner);
            if (topEdge && rightEdge) walls.Quad(new Rect(cell.x + 1 - rim, cell.y + 1 - rim, rim, rim), corner);
            if (bottomEdge && leftEdge) walls.Quad(new Rect(cell.x, cell.y, rim, rim), corner);
            if (bottomEdge && rightEdge) walls.Quad(new Rect(cell.x + 1 - rim, cell.y, rim, rim), corner);
        }
        foreach (DungeonRoom room in layout.Rooms)
        {
            int left = room.FloorTiles.Min(c => c.x), right = room.FloorTiles.Max(c => c.x);
            int bottom = room.FloorTiles.Min(c => c.y), top = room.FloorTiles.Max(c => c.y);
            int cx = (left + right) / 2, cy = (bottom + top) / 2;
            // Sparse accents leave the fighting area and all corridors clear.
            floor.Quad(new Rect(cx - 1, cy - 1, 3, 3), Tile(theme.usesCampaignAtlas ? 3 : room.Type == RoomType.Boss ? 10 : 8));
            foreach (int x in new[] { left + 3, right - 3 })
            {
                Vector2Int at = new(x, top - 1);
                if (theme.usesCampaignAtlas)
                    while (at.y > top - 7 && (!room.FloorTiles.Contains(at) || !room.FloorTiles.Contains(at + Vector2Int.right))) at.y--;
                if (!room.FloorTiles.Contains(at)) continue;
                owner.SolidProp(layout, props, "Runic Pillar", theme.usesCampaignAtlas ? 8 : 0,
                    new Rect(x - 0.3f, at.y, 1.6f, 2.1f),
                    new Rect(x - 0.02f, at.y + 0.02f, 1.04f, 0.48f), reservedCells);
                // Legacy torch frames are transient Sprites: animate in Play Mode,
                // not serialized into the saved editor-only mesh preview.
                float torchX = x + 0.5f + (x < cx ? 1.6f : -1.6f);
                int wallTop = top;
                if (theme.usesCampaignAtlas)
                {
                    var column = room.FloorTiles.Where(cell => cell.x == Mathf.FloorToInt(torchX));
                    if (!column.Any()) continue;
                    wallTop = column.Max(cell => cell.y);
                }
                if (!layout.FloorTiles.Contains(new Vector2Int(Mathf.FloorToInt(torchX), wallTop + 1)))
                {
                    if (Application.isPlaying)
                    {
                        var torch = DungeonDecorator.CreateWallTorch(new Vector3(torchX, wallTop + 1.45f, 0), root.transform,
                            theme.arcaneTorch, theme.torchLightColor);
                        if (torch != null)
                        {
                            Bounds b = torch.GetComponent<SpriteRenderer>().bounds;
                            owner.decorImageBounds.Add(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y));
                        }
                    }
                    // A wall bracket separate from the pillar avoids a floating brazier silhouette.
                    if (!theme.usesCampaignAtlas)
                        walls.Quad(new Rect(torchX - 0.08f, top + 1.13f, 0.16f, 0.33f), FromTop(664, 139, 20, 90));
                }
            }
            // Không sinh rương trang trí hoặc footprint chặn đường; giữ nguyên phòng Treasure.
            if (room.Type == RoomType.Boss)
                owner.SolidProp(layout, props, "Guardian Statue", theme.usesCampaignAtlas ? 11 : 10,
                    new Rect(cx - 0.5f, top - 2, 1.8f, 2.4f),
                    new Rect(cx - 0.32f, top - 1.9f, 1.44f, 0.55f), reservedCells);
            if (theme.usesCampaignAtlas && (room.Type == RoomType.Start || room.Type == RoomType.Treasure || room.Type == RoomType.Boss))
                owner.SolidProp(layout, props, "Theme Relic", 10,
                    new Rect(left + 2, top - 3, 1.8f, 2f),
                    new Rect(left + 2.15f, top - 2.9f, 1.5f, 0.6f), reservedCells);
        }
        owner.Emit("Arcane Floor", floor, theme.arcaneFloor, -1);
        owner.Emit("Arcane Walls", walls, theme.arcaneWalls, 0);
        owner.Emit("Arcane Props", props, theme.arcaneProps, 2);
        owner.AppendAmbience(layout, theme);
        return root;
    }

    public static GameObject BuildAmbienceOnly(DungeonLayout layout, CourtyardTheme theme)
    {
        var root = new GameObject("Pixel Dungeon Ambience");
        root.AddComponent<ArcaneCitadelGeometry>().AppendAmbience(layout, theme);
        return root;
    }

    private void AppendAmbience(DungeonLayout layout, CourtyardTheme theme)
    {
        var set = Resources.Load<DungeonAmbienceSet>("DungeonAmbienceSet");
        var palette = set == null ? null : set.ForTheme(theme);
        if (palette == null) return; // Asset cũ vẫn hoạt động nếu chưa cài bộ trang trí.
        var fire = new Batch();
        var souls = new Batch();
        var details = new Batch();
        foreach (var room in layout.Rooms)
        {
            // Chỉ chọn mép tường trên thực tế, không suy đoán từ bounding rectangle của phòng.
            var edge = room.FloorTiles.Where(c => !layout.FloorTiles.Contains(c + Vector2Int.up) &&
                room.FloorTiles.Contains(c + Vector2Int.down) && (c - room.SpawnPoint).sqrMagnitude >= 16)
                .OrderBy(c => c.x).ThenBy(c => c.y).ToList();
            if (edge.Count < 4) continue;
            int area = room.FloorTiles.Count;
            float centerX = (room.FloorTiles.Min(c => c.x) + room.FloorTiles.Max(c => c.x) + 1) * .5f;
            // Map gốc đã có đuốc tường: chỉ bổ sung mạng nhện/xương/linh hồn, không thêm chậu lửa.
            int fireCount = theme == null ? 0 : area < 120 ? 1 : 2;
            for (int side = 0; side < fireCount; side++)
            {
                var target = edge[side == 0 ? edge.Count / 5 : edge.Count * 4 / 5];
                // Hai vùng bố cục riêng: không tìm chỗ trống bằng cách dồn cả hai chậu sang một bên.
                var candidates = edge.Where(c => fireCount == 1 || (side == 0 ? c.x + .5f < centerX : c.x + .5f > centerX))
                    .Where(c => (c - target).sqrMagnitude <= 25).OrderBy(c => (c - target).sqrMagnitude);
                foreach (var cell in candidates)
                {
                    var image = new Rect(cell.x - .1f, cell.y + .2f, 1.2f, 1.6f);
                    if (fireImageBounds.Any(other => Vector2.Distance(image.center, other.center) < 5f)) continue;
                    if (!ReserveDecorImage(image, 1.25f)) continue;
                    fireImageBounds.Add(image);
                    fire.Quad(image, CellUv(0));
                    break; // Không có vị trí trống thì bỏ qua, không ép chồng lên đồ khác.
                }
            }
            // Chi tiết thấp chuyển sang hai mép bên để phần trên không gánh toàn bộ trang trí.
            float minY = room.FloorTiles.Min(c => c.y), maxY = room.FloorTiles.Max(c => c.y);
            int accentCount = area >= 180 ? 2 : 1;
            for (int side = 0; side < accentCount; side++)
            {
                Vector2Int outward = side == 0 ? Vector2Int.left : Vector2Int.right;
                float targetY = Mathf.Lerp(minY, maxY, side == 0 ? .35f : .65f);
                var candidates = room.FloorTiles.Where(c => !layout.FloorTiles.Contains(c + outward) &&
                    layout.FloorTiles.Contains(c + Vector2Int.up) && layout.FloorTiles.Contains(c + Vector2Int.down) &&
                    (c - room.SpawnPoint).sqrMagnitude >= 16).OrderBy(c => Mathf.Abs(c.y - targetY)).ThenBy(c => c.x);
                foreach (var cell in candidates)
                {
                    var image = new Rect(cell.x + (side == 0 ? .05f : -.1f), cell.y + .05f, 1.05f, 1.25f);
                    if (!ReserveDecorImage(image, .55f)) continue;
                    details.Quad(image, CellUv(palette.accentCell)); break;
                }
            }
            int soulCount = Mathf.Min(palette.soulsPerRoom, room.Type == RoomType.Boss && area >= 250 ? 2 : 1);
            for (int i = 0; i < soulCount; i++)
            {
                var target = edge[(i + 1) * edge.Count / (soulCount + 1)];
                foreach (var cell in edge.Where(c => (c - target).sqrMagnitude <= 16).OrderBy(c => (c - target).sqrMagnitude))
                {
                    var image = new Rect(cell.x, cell.y + 1.15f, .85f, 1.15f);
                    var floatingBounds = new Rect(image.x, image.y - .12f, image.width, image.height + .24f);
                    if (!ReserveDecorImage(floatingBounds)) continue;
                    souls.Quad(image, CellUv(4)); break;
                }
            }
            for (int i = 0; i < Mathf.Min(palette.websPerRoom, area >= 160 ? 2 : 1); i++)
            {
                var cell = edge[i == 0 ? 0 : i == 1 ? edge.Count - 1 : edge.Count / 2];
                var image = new Rect(cell.x - .25f, cell.y + .3f, 1.5f, 1.5f);
                if (ReserveDecorImage(image)) details.Quad(image, CellUv(8 + i), mirrorX: i == 1);
            }
        }
        if (palette.fire != null && fire.vertices.Count > 0) Emit("Ambient Fire", fire, palette.fire, 3);
        if (palette.souls != null && souls.vertices.Count > 0) Emit("Ambient Souls", souls, palette.souls, 4);
        if (palette.details != null) Emit("Ambient Details", details, palette.details, 3);
    }

    private bool ReserveDecorImage(Rect image, float gap = .45f)
    {
        var padded = new Rect(image.xMin - gap, image.yMin - gap, image.width + gap * 2, image.height + gap * 2);
        if (decorImageBounds.Any(existing => padded.Overlaps(existing))) return false;
        // Đồ đặt sau chậu cũng phải giữ khoảng thoáng, không chỉ chậu tránh đồ đặt trước.
        var fireClearance = new Rect(image.xMin - 1.25f, image.yMin - 1.25f, image.width + 2.5f, image.height + 2.5f);
        if (fireImageBounds.Any(existing => fireClearance.Overlaps(existing))) return false;
        decorImageBounds.Add(image);
        return true;
    }

    private static bool Outside(Vector2Int cell, HashSet<Vector2Int> floor, HashSet<Vector2Int> border) =>
        !floor.Contains(cell) && !border.Contains(cell);

    private void SolidProp(DungeonLayout layout, Batch batch, string label, int atlasIndex,
        Rect image, Rect feet, HashSet<Vector2Int> reservedCells)
    {
        var cells = new List<Vector2Int>();
        for (int x = Mathf.FloorToInt(feet.xMin); x <= Mathf.FloorToInt(feet.xMax - 0.001f); x++)
        for (int y = Mathf.FloorToInt(feet.yMin); y <= Mathf.FloorToInt(feet.yMax - 0.001f); y++)
        {
            var cell = new Vector2Int(x, y);
            if (!layout.FloorTiles.Contains(cell) || (reservedCells != null && reservedCells.Contains(cell))) return;
            cells.Add(cell);
        }
        Rect uv = artTheme.usesCampaignAtlas ? artTheme.AtlasUv(atlasIndex) : CellUv(atlasIndex);
        if (artTheme.usesCampaignAtlas && artTheme.arcaneProps.mainTexture != null)
        {
            var texture = artTheme.arcaneProps.mainTexture;
            float aspect = uv.width * texture.width / (uv.height * texture.height);
            float width = Mathf.Min(image.width, image.height * aspect);
            float height = width / aspect;
            image = new Rect(image.center.x - width * 0.5f, image.yMin, width, height);
        }
        batch.Quad(image, uv);
        decorImageBounds.Add(image); // Giữ cả hình cao của bình/tượng, không chỉ collider ở chân.
        var blocker = new GameObject(label + " Footprint");
        blocker.transform.SetParent(transform, false);
        blocker.transform.position = new Vector3(feet.center.x, feet.center.y, 0);
        var collider = blocker.AddComponent<BoxCollider2D>();
        collider.size = feet.size;
        // NavMeshPlus uses render geometry in this project. A modifier VOLUME is
        // collected independently of SpriteRenderer/Collider collection mode.
        var volume = blocker.AddComponent<NavMeshPlus.Components.NavMeshModifierVolume>();
        volume.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
        volume.size = new Vector3(feet.width + 0.6f, feet.height + 0.6f, 4);
        if (reservedCells != null) foreach (Vector2Int cell in cells) reservedCells.Add(cell);
    }

    private static Rect CellUv(int index) => new((index % 4) * 0.25f + 0.004f,
        1f - (index / 4 + 1) * 0.25f + 0.004f, 0.242f, 0.242f);

    private static Rect PixelSafeUv(Rect uv, Texture texture)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0) return uv;
        float insetX = 0.5f / texture.width;
        float insetY = 0.5f / texture.height;
        return Rect.MinMaxRect(uv.xMin + insetX, uv.yMin + insetY,
            uv.xMax - insetX, uv.yMax - insetY);
    }

    // The new wall sheet is 1254 square; these rectangles exclude transparent gutters.
    private static Rect FromTop(float x, float y, float width, float height) =>
        new(x / 1254f, 1f - (y + height) / 1254f, width / 1254f, height / 1254f);

    private void Emit(string label, Batch batch, Material material, int order)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var mesh = new Mesh { name = label + " Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(batch.vertices);
        mesh.SetUVs(0, batch.uvs);
        mesh.SetColors(batch.colors);
        mesh.SetTriangles(batch.indices, 0);
        mesh.RecalculateBounds();
        // Shader linh hồn dịch đỉnh tối đa 0.12 world unit; mở bounds để không bị culling nhấp nháy.
        var bounds = mesh.bounds;
        bounds.Expand(0.3f);
        mesh.bounds = bounds;
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (Application.isPlaying) ownedMeshes.Add(mesh);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
    }

    private sealed class Batch
    {
        public readonly List<Vector3> vertices = new();
        public readonly List<Vector2> uvs = new();
        public readonly List<Color> colors = new();
        public readonly List<int> indices = new();
        public void Quad(Rect world, Rect uv, bool mirrorX = false, bool rotateUv = false)
        {
            int n = vertices.Count;
            vertices.Add(new Vector3(world.xMin, world.yMin, 0));
            vertices.Add(new Vector3(world.xMin, world.yMax, 0));
            vertices.Add(new Vector3(world.xMax, world.yMax, 0));
            vertices.Add(new Vector3(world.xMax, world.yMin, 0));
            float uLeft = mirrorX ? uv.xMax : uv.xMin, uRight = mirrorX ? uv.xMin : uv.xMax;
            if (rotateUv)
            {
                uvs.Add(new Vector2(uLeft, uv.yMax)); uvs.Add(new Vector2(uRight, uv.yMax));
                uvs.Add(new Vector2(uRight, uv.yMin)); uvs.Add(new Vector2(uLeft, uv.yMin));
            }
            else
            {
                uvs.Add(new Vector2(uLeft, uv.yMin)); uvs.Add(new Vector2(uLeft, uv.yMax));
                uvs.Add(new Vector2(uRight, uv.yMax)); uvs.Add(new Vector2(uRight, uv.yMin));
            }
            for (int i = 0; i < 4; i++) colors.Add(Color.white);
            indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
    }
}
