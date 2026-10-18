using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;

/// <summary>Host visual snapshots; client views contain no gameplay scripts or colliders.</summary>
public sealed class LanWorldReplication
{
    private readonly LanAssetCatalog catalog;
    private readonly Dictionary<int, Sprite> clientSprites = new Dictionary<int, Sprite>();
    private readonly HashSet<int> sentSprites = new HashSet<int>();
    private readonly HashSet<int> sentTextures = new HashSet<int>();
    private readonly Dictionary<int, string> encodedTextures = new Dictionary<int, string>();
    private readonly Dictionary<int, Texture2D> clientTextures = new Dictionary<int, Texture2D>();
    private readonly Dictionary<int, SpriteRenderer> clientViews = new Dictionary<int, SpriteRenderer>();
    private readonly Dictionary<int, Light2D> clientLights = new Dictionary<int, Light2D>();
    private readonly Dictionary<int, LanVisual> targets = new Dictionary<int, LanVisual>();
    private readonly List<Sprite> createdSprites = new List<Sprite>();
    private readonly List<int> removed = new List<int>();
    private readonly Transform root;
    private Sprite whiteSprite;
    public int RuntimeTextureCount => clientTextures.Count;
    private readonly Dictionary<int, MeshFilter> clientMeshes = new Dictionary<int, MeshFilter>();
    public int ReplicatedMeshCount => clientMeshes.Count;

    public LanWorldReplication(LanAssetCatalog catalog, Transform root)
    { this.catalog = catalog; this.root = root; }

    public void ResetPeerCache() { sentSprites.Clear(); sentTextures.Clear(); encodedTextures.Clear(); }

    public LanTilemap[] CaptureMap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        var result = new List<LanTilemap>();
        foreach (Tilemap map in tilemaps)
        {
            var cells = new List<Vector3Int>();
            var tiles = new List<int>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                TileBase tile = map.GetTile(cell);
                if (tile == null) continue;
                int id = catalog.Id(tile);
                if (id < 0) throw new System.InvalidOperationException("Tile chưa có trong LAN catalog: " + tile.name);
                cells.Add(cell); tiles.Add(id);
            }
            result.Add(new LanTilemap { name = map.name, visible = map.GetComponent<TilemapRenderer>().enabled,
                cells = cells.ToArray(), tiles = tiles.ToArray() });
        }
        return result.ToArray();
    }

    public void ApplyMap(LanTilemap[] maps)
    {
        if (maps == null || maps.Length > 16) throw new System.IO.InvalidDataException("Invalid LAN map.");
        Tilemap[] local = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (Tilemap map in local) map.ClearAllTiles();
        foreach (LanTilemap data in maps)
        {
            Tilemap map = System.Array.Find(local, candidate => candidate.name == data.name);
            if (map == null || data.cells == null || data.tiles == null || data.cells.Length != data.tiles.Length || data.cells.Length > 100000)
                throw new System.IO.InvalidDataException("LAN map không tương thích.");
            TileBase[] tiles = new TileBase[data.tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                if (data.tiles[i] < 0 || data.tiles[i] >= catalog.tiles.Length) throw new System.IO.InvalidDataException("Invalid tile ID.");
                tiles[i] = catalog.tiles[data.tiles[i]];
            }
            map.SetTiles(data.cells, tiles);
            map.GetComponent<TilemapRenderer>().enabled = data.visible;
        }
        foreach (var view in clientViews.Values) if (view != null) Object.Destroy(view.gameObject);
        clientViews.Clear(); targets.Clear();
        ClearLights();
    }

    public void CaptureVisuals(LanMessage message)
    {
        var frames = new List<LanMeshFrame>();
        foreach (var plate in Object.FindObjectsByType<ArcaneSpikeTrapVisual>(FindObjectsSortMode.None))
        {
            var filter = plate.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                frames.Add(new LanMeshFrame { id = filter.GetInstanceID(), uv = filter.sharedMesh.uv });
        }
        message.meshFrames = frames.ToArray();
        var definitions = new List<LanSpriteDefinition>();
        var visuals = new List<LanVisual>();
        foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.sprite == null || !renderer.gameObject.activeInHierarchy) continue;
            Sprite sprite = renderer.sprite;
            int spriteId = sprite.GetInstanceID();
            if (sentSprites.Add(spriteId))
            {
                var definition = new LanSpriteDefinition
                {
                    id = spriteId, sprite = catalog.Id(sprite), texture = catalog.Id(sprite.texture), rect = sprite.rect,
                    pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height),
                    pixelsPerUnit = sprite.pixelsPerUnit
                };
                // Skill VFX also create padded/fallback textures at runtime. Ship their pixels
                // once instead of replacing them with a white rectangle on the Client.
                if (definition.sprite < 0 && definition.texture < 0 && sprite.texture.isReadable)
                {
                    int textureId = sprite.texture.GetInstanceID();
                    definition.runtimeTexture = textureId;
                    if (sentTextures.Add(textureId))
                    {
                        if (!encodedTextures.TryGetValue(textureId, out string png))
                        {
                            if (sprite.texture.width > 2048 || sprite.texture.height > 2048)
                                throw new System.InvalidOperationException("Runtime LAN texture exceeds 2048 pixels.");
                            byte[] bytes = sprite.texture.EncodeToPNG();
                            if (bytes.Length > 1024 * 1024) throw new System.InvalidOperationException("Runtime LAN texture exceeds 1 MiB.");
                            png = System.Convert.ToBase64String(bytes);
                            encodedTextures[textureId] = png;
                        }
                        definition.png = png;
                    }
                }
                definitions.Add(definition);
            }
            visuals.Add(new LanVisual
            {
                id = renderer.GetInstanceID(), name = renderer.gameObject.name, gameLayer = renderer.gameObject.layer,
                sprite = spriteId, position = renderer.transform.position,
                rotation = renderer.transform.rotation, scale = renderer.transform.lossyScale, color = renderer.color,
                flipX = renderer.flipX, flipY = renderer.flipY, layer = renderer.sortingLayerID,
                order = renderer.sortingOrder, material = catalog.Id(renderer.sharedMaterial)
            });
        }
        message.definitions = definitions.ToArray();
        message.visuals = visuals.ToArray();
        var lights = new List<LanLight>();
        foreach (Light2D light in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            // Player/darkness lighting is managed separately by the session.
            if (!light.isActiveAndEnabled || light.lightType != Light2D.LightType.Point ||
                (light.GetComponent<AnimatedDecor>() == null && light.GetComponentInParent<Boss3DVisual>() == null)) continue;
            lights.Add(new LanLight
            {
                id = light.GetInstanceID(), name = light.name, position = light.transform.position, scale = light.transform.lossyScale,
                color = light.color, intensity = light.intensity,
                innerRadius = light.pointLightInnerRadius, outerRadius = light.pointLightOuterRadius
            });
        }
        message.lights = lights.ToArray();
    }

    public void ApplyVisuals(LanMessage message)
    {
        if (message.meshFrames != null)
        {
            if (message.meshFrames.Length > 256) throw new System.IO.InvalidDataException("Too many trap mesh frames.");
            foreach (var frame in message.meshFrames)
                if (frame.uv != null && frame.uv.Length == 4 && clientMeshes.TryGetValue(frame.id, out MeshFilter filter))
                    filter.sharedMesh.uv = frame.uv;
        }
        ApplyLights(message.lights);
        if (message.definitions != null)
            foreach (LanSpriteDefinition data in message.definitions)
            {
                if (clientSprites.ContainsKey(data.id)) continue;
                Sprite sprite;
                if (!string.IsNullOrEmpty(data.png) && !clientTextures.ContainsKey(data.runtimeTexture))
                {
                    if (data.png.Length > 1400000) throw new System.IO.InvalidDataException("Runtime texture payload too large.");
                    byte[] bytes = System.Convert.FromBase64String(data.png);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!texture.LoadImage(bytes) || texture.width > 2048 || texture.height > 2048)
                    { Object.Destroy(texture); throw new System.IO.InvalidDataException("Invalid runtime texture."); }
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    clientTextures[data.runtimeTexture] = texture;
                }
                if (data.sprite >= 0 && data.sprite < catalog.sprites.Length) sprite = catalog.sprites[data.sprite];
                else if (data.texture >= 0 && data.texture < catalog.textures.Length)
                {
                    sprite = Sprite.Create(catalog.textures[data.texture], data.rect, data.pivot, data.pixelsPerUnit);
                    createdSprites.Add(sprite);
                }
                else if (clientTextures.TryGetValue(data.runtimeTexture, out Texture2D runtimeTexture))
                {
                    sprite = Sprite.Create(runtimeTexture, data.rect, data.pivot, data.pixelsPerUnit);
                    createdSprites.Add(sprite);
                }
                else
                {
                    if (whiteSprite == null)
                    {
                        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
                        createdSprites.Add(whiteSprite);
                    }
                    sprite = whiteSprite;
                }
                clientSprites[data.id] = sprite;
            }
        targets.Clear();
        if (message.visuals == null) return;
        foreach (LanVisual data in message.visuals)
        {
            targets[data.id] = data;
            if (!clientViews.TryGetValue(data.id, out SpriteRenderer renderer))
            {
                GameObject view = new GameObject("LAN/" + data.name);
                view.transform.SetParent(root, false);
                renderer = view.AddComponent<SpriteRenderer>();
                renderer.transform.position = data.position;
                clientViews[data.id] = renderer;
            }
            clientSprites.TryGetValue(data.sprite, out Sprite sprite);
            renderer.sprite = sprite;
            renderer.gameObject.layer = Mathf.Clamp(data.gameLayer, 0, 31);
            renderer.color = data.color;
            renderer.flipX = data.flipX; renderer.flipY = data.flipY;
            renderer.sortingLayerID = data.layer; renderer.sortingOrder = data.order;
            if (data.material >= 0 && data.material < catalog.materials.Length)
                renderer.sharedMaterial = catalog.materials[data.material];
            renderer.transform.localScale = data.scale;
            renderer.transform.rotation = data.rotation;
        }
        removed.Clear();
        foreach (var pair in clientViews) if (!targets.ContainsKey(pair.Key)) removed.Add(pair.Key);
        foreach (int id in removed) { Object.Destroy(clientViews[id].gameObject); clientViews.Remove(id); }
    }

    public void Interpolate()
    {
        float factor = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        foreach (var pair in targets)
        {
            if (!clientViews.TryGetValue(pair.Key, out SpriteRenderer view) || view == null) continue;
            view.transform.position = Vector3.Distance(view.transform.position, pair.Value.position) > 4f
                ? pair.Value.position : Vector3.Lerp(view.transform.position, pair.Value.position, factor);
        }
    }

    // Map meshes are sent once per level; only four UVs per moving trap are in snapshots.
    public LanMesh[] CaptureMeshes()
    {
        var result = new List<LanMesh>();
        foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (filter.GetComponentInParent<ArcaneCitadelGeometry>() == null && filter.GetComponent<ArcaneSpikeTrapVisual>() == null) continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            var mesh = filter.sharedMesh;
            if (renderer == null || !renderer.enabled || mesh == null) continue;
            int material = catalog.Id(renderer.sharedMaterial);
            if (material < 0) throw new System.InvalidOperationException("Missing LAN map material: " + renderer.sharedMaterial.name);
            result.Add(new LanMesh { id = filter.GetInstanceID(), name = filter.name, material = material,
                layer = renderer.sortingLayerID, order = renderer.sortingOrder,
                position = filter.transform.position, scale = filter.transform.lossyScale, rotation = filter.transform.rotation,
                vertices = mesh.vertices, triangles = mesh.triangles, uv = mesh.uv });
        }
        return result.ToArray();
    }

    public void ApplyMeshes(LanMesh[] meshes)
    {
        ClearMeshes();
        if (meshes == null) return;
        if (meshes.Length > 256) throw new System.IO.InvalidDataException("Too many LAN map meshes.");
        int totalVertices = 0;
        foreach (var data in meshes)
        {
            if (data == null || data.vertices == null || data.uv == null || data.triangles == null ||
                data.vertices.Length != data.uv.Length || data.triangles.Length % 3 != 0 ||
                data.vertices.Length > 100000 || data.triangles.Length > 150000 ||
                data.material < 0 || data.material >= catalog.materials.Length || clientMeshes.ContainsKey(data.id))
                throw new System.IO.InvalidDataException("Invalid LAN map mesh.");
            totalVertices += data.vertices.Length;
            if (totalVertices > 200000) throw new System.IO.InvalidDataException("LAN mesh budget exceeded.");
            foreach (int index in data.triangles) if (index < 0 || index >= data.vertices.Length)
                throw new System.IO.InvalidDataException("Invalid LAN mesh index.");
            foreach (Vector3 vertex in data.vertices)
                if (float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                    float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z))
                    throw new System.IO.InvalidDataException("Invalid LAN vertex.");
            var view = new GameObject("LAN Map/" + data.name);
            view.transform.SetParent(root, false);
            view.transform.SetPositionAndRotation(data.position, data.rotation);
            view.transform.localScale = data.scale;
            var mesh = new Mesh { name = data.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = data.vertices; mesh.triangles = data.triangles; mesh.uv = data.uv;
            var colors = new Color[data.vertices.Length]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors; mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(0.3f); mesh.bounds = bounds;
            var filter = view.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = view.AddComponent<MeshRenderer>(); renderer.sharedMaterial = catalog.materials[data.material];
            renderer.sortingLayerID = data.layer; renderer.sortingOrder = data.order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            clientMeshes.Add(data.id, filter);
        }
    }

    private void ClearMeshes()
    {
        foreach (var filter in clientMeshes.Values)
        {
            if (filter == null) continue;
            if (filter.sharedMesh != null) Object.Destroy(filter.sharedMesh);
            filter.gameObject.SetActive(false);
            Object.Destroy(filter.gameObject);
        }
        clientMeshes.Clear();
    }

    public void Dispose()
    {
        ClearMeshes();
        ClearLights();
        foreach (Sprite sprite in createdSprites) if (sprite != null) Object.Destroy(sprite);
        foreach (Texture2D texture in clientTextures.Values) if (texture != null) Object.Destroy(texture);
        foreach (var view in clientViews.Values) if (view != null) Object.Destroy(view.gameObject);
        createdSprites.Clear(); clientSprites.Clear(); clientViews.Clear(); targets.Clear();
        clientTextures.Clear(); encodedTextures.Clear(); sentSprites.Clear(); sentTextures.Clear();
    }

    private void ApplyLights(LanLight[] lights)
    {
        if (lights == null) { ClearLights(); return; }
        if (lights.Length > 1024) throw new System.IO.InvalidDataException("Too many LAN lights.");
        var active = new HashSet<int>();
        foreach (LanLight data in lights)
        {
            active.Add(data.id);
            if (!clientLights.TryGetValue(data.id, out Light2D light))
            {
                var view = new GameObject("LAN Light/" + data.name);
                view.transform.SetParent(root, false);
                light = view.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                clientLights[data.id] = light;
            }
            light.transform.position = data.position;
            light.transform.localScale = data.scale;
            light.color = data.color;
            light.intensity = Mathf.Clamp(data.intensity, 0f, 10f);
            light.pointLightOuterRadius = Mathf.Clamp(data.outerRadius, 0.01f, 50f);
            light.pointLightInnerRadius = Mathf.Clamp(data.innerRadius, 0f, light.pointLightOuterRadius);
        }
        removed.Clear();
        foreach (var pair in clientLights) if (!active.Contains(pair.Key)) removed.Add(pair.Key);
        foreach (int id in removed) { Object.Destroy(clientLights[id].gameObject); clientLights.Remove(id); }
    }

    private void ClearLights()
    {
        foreach (Light2D light in clientLights.Values) if (light != null) Object.Destroy(light.gameObject);
        clientLights.Clear();
    }
}
