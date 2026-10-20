using System.Collections.Generic;
using UnityEngine;

/// <summary>Sắp lớp theo chân trên mặt đất, không theo chiều cao frame animation.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class WorldDepthSort : MonoBehaviour
{
    // Dành các order thấp cho sàn, bẫy và decal. Phạm vi đủ cho toàn bộ campaign.
    public static int OrderAt(float groundY) => Mathf.Clamp(16000 - Mathf.RoundToInt(groundY * 32f), 100, 29000);

    [SerializeField] private float localGroundY;
    [SerializeField] private int referenceOrder;
    private readonly List<SpriteRenderer> sprites = new();
    private readonly Dictionary<SpriteRenderer, int> offsets = new();

    public static WorldDepthSort Attach(GameObject target, float worldGroundY)
    {
        var sorter = target.GetComponent<WorldDepthSort>();
        if (sorter != null) return sorter;
        sorter = target.AddComponent<WorldDepthSort>();
        sorter.localGroundY = target.transform.InverseTransformPoint(new Vector3(target.transform.position.x, worldGroundY, target.transform.position.z)).y;
        var primary = target.GetComponent<SpriteRenderer>();
        sorter.referenceOrder = primary != null ? primary.sortingOrder : 0;
        return sorter;
    }

    public static void AttachActor(GameObject actor)
    {
        // Collider của thân là mốc đất ổn định; không đổi khi animation nhảy/lắc.
        var body = actor.GetComponent<Collider2D>();
        Attach(actor, body != null ? body.transform.TransformPoint(body.offset).y : actor.transform.position.y);
    }

    private void LateUpdate() => RefreshOrder();

    public void RefreshOrder()
    {
        int order = OrderAt(transform.TransformPoint(new Vector3(0, localGroundY, 0)).y);
        // Nhận cả visual/VFX được tạo sau Start, không cấp phát mảng mỗi frame.
        sprites.Clear();
        GetComponentsInChildren(true, sprites);
        foreach (var sprite in sprites)
        {
            if (sprite.GetComponentInParent<WorldDepthSort>() != this) continue;
            if (!offsets.TryGetValue(sprite, out int offset))
            {
                // VFX mới có thể đã lấy order động từ renderer của chủ nhân.
                int baseline = sprite.sortingOrder >= 100 ? order : referenceOrder;
                offset = Mathf.Clamp(sprite.sortingOrder - baseline, -8, 8);
                offsets.Add(sprite, offset);
            }
            sprite.sortingOrder = order + offset;
        }
    }
}
