using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class DecorationInfo
{
    public int id;
    public string kind;
    public Rect bounds;
    public Vector2 point;
    public bool wall;
}

// Chỉ đánh dấu hoa văn trên nền; các vật sprite/mesh còn lại được thu thập trực tiếp.
public sealed class DecorationInspection : MonoBehaviour
{
    public Rect image;
    public static void Mark(Transform parent, Rect image)
    {
        if (!Application.isPlaying) return;
        var go = new GameObject("Hoa văn có thể quan sát");
        go.transform.SetParent(parent, false);
        go.AddComponent<DecorationInspection>().image = image;
    }

    private static bool IsDecoration(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name == "Dungeon Decorations" || t.name == "Courtyard Atlas Details" ||
                t.GetComponent<ArcaneCitadelGeometry>() != null) return true;
        return false;
    }

    public static DecorationInfo[] Collect()
    {
        var entries = new List<DecorationInfo>();
        foreach (var sprite in FindObjectsOfType<SpriteRenderer>())
        {
            if (!IsDecoration(sprite.transform) || sprite.sprite == null) continue;
            // Hào quang là một phần của đuốc, không tạo thêm một "đồ vật" trùng đuốc.
            if (sprite.GetComponentInParent<AnimatedDecor>() != null && sprite.GetComponent<AnimatedDecor>() == null) continue;
            var b = sprite.bounds;
            Add(entries, Classify(sprite.name + " " + sprite.sprite.name),
                Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y));
        }
        foreach (var filter in FindObjectsOfType<MeshFilter>())
        {
            if (!IsDecoration(filter.transform) || filter.sharedMesh == null ||
                filter.name == "Arcane Floor" || filter.name == "Arcane Walls") continue;
            var vertices = filter.sharedMesh.vertices;
            var uv = filter.sharedMesh.uv;
            for (int i = 0; i + 3 < vertices.Length; i += 4)
            {
                var b = new Bounds(filter.transform.TransformPoint(vertices[i]), Vector3.zero);
                for (int j = 1; j < 4; j++) b.Encapsulate(filter.transform.TransformPoint(vertices[i + j]));
                string kind = Classify(filter.name);
                if (filter.name.StartsWith("Ambient Accent") && uv.Length > i)
                {
                    int cell = Mathf.FloorToInt(uv[i].x * 4) + (3 - Mathf.FloorToInt(uv[i].y * 4)) * 4;
                    kind = cell == 12 ? "mushroom" : cell == 13 ? "crystal" : cell == 14 ? "candle" : "bones";
                }
                Add(entries, kind, Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y));
            }
        }
        foreach (var marker in FindObjectsOfType<DecorationInspection>()) Add(entries, "inlay", marker.image);
        var result = entries.OrderBy(e => e.point.y).ThenBy(e => e.point.x).ThenBy(e => e.kind).ToArray();
        for (int i = 0; i < result.Length; i++) result[i].id = i + 1;
        return result;
    }

    private static void Add(List<DecorationInfo> list, string kind, Rect image)
    {
        bool wall = kind == "torch" || kind == "banner" || kind == "web" || kind == "soul" || kind == "plaque";
        list.Add(new DecorationInfo { kind = kind, bounds = image, wall = wall,
            // Quan sát vật treo từ phía sàn, không buộc nhân vật đứng trong tường.
            point = new Vector2(image.center.x, kind == "inlay" ? image.center.y :
                image.yMin + Mathf.Min(.25f, image.height * .2f) - (wall ? .65f : 0)) });
    }

    public static float Range(DecorationInfo item) => item.wall ? 3f : 2.25f;

    private static string Classify(string label)
    {
        string s = label.ToLowerInvariant();
        if (s.Contains("torch")) return "torch";
        if (s.Contains("banner") || s.Contains("flag")) return "banner";
        if (s.Contains("ambient details") || s.Contains("web")) return "web";
        if (s.Contains("soul")) return "soul";
        if (s.Contains("fire") || s.Contains("brazier")) return "brazier";
        if (s.Contains("throne")) return "throne";
        if (s.Contains("statue")) return "statue";
        if (s.Contains("pillar")) return "pillar";
        if (s.Contains("inlay")) return "inlay";
        if (s.Contains("stair")) return "stairs";
        if (s.Contains("plaque")) return "plaque";
        if (s.Contains("crate") || s.Contains("box") || s.Contains("barrel")) return "storage";
        if (s.Contains("bone") || s.Contains("skull") || s.Contains("remains")) return "bones";
        if (s.Contains("pot") || s.Contains("vase") || s.Contains("urn")) return "pottery";
        if (s.Contains("relic")) return "relic";
        return "supplies";
    }

    public static string Title(string kind) => kind switch {
        "bones" => "Hài cốt", "torch" => "Đuốc trên tường", "banner" => "Lá cờ cũ",
        "web" => "Mạng nhện", "soul" => "Bóng hồn", "brazier" => "Chậu lửa",
        "mushroom" => "Cụm nấm", "crystal" => "Tinh thể", "candle" => "Nến",
        "throne" => "Ngai đá", "statue" => "Tượng canh giữ", "pillar" => "Cột khắc dấu",
        "inlay" => "Hoa văn trên nền", "stairs" => "Bậc thềm cũ", "plaque" => "Phù điêu",
        "storage" => "Thùng chứa đồ", "pottery" => "Bình gốm", "relic" => "Di vật",
        _ => "Vật dụng bỏ lại"
    };

    public static string Describe(string kind, int level, bool fought, bool kingDead,
        bool knowsInscription = false, bool knowsDais = false, bool solvedDais = false)
    {
        if (kind == "bones") return fought
            ? "Những mẩu xương nằm lẫn trong bụi… Người này cũng đã không vượt qua được bọn quái vật sao?"
            : "Có người đã chết ở đây… Không còn thứ gì cho biết họ là ai. Phải cẩn thận hơn.";
        if (kind == "throne") return kingDead
            ? "Ngai đã trống rồi. Ta chẳng cần thứ này… Đường về mới là điều ta muốn."
            : "Một chiếc ngai giữa mê cung… Kẻ ngồi đây đã giữ bao nhiêu người ở lại?";
        return kind switch {
            "torch" => level == 6 ? "Đuốc chỉ soi được một khoảng nhỏ. Ta nên nhớ chỗ sáng này để còn tìm đường quay lại."
                : "Đuốc vẫn cháy mà chẳng thấy ai trông coi. Nơi này không hẳn đã bị bỏ hoang.",
            "banner" => "Vải đã sờn, huy hiệu cũng phai gần hết. Chủ nhân của nó giờ còn ở đâu?",
            "web" => "Mạng nhện bám kín góc tường. Có vẻ đã lâu không ai chạm vào chỗ này.",
            "soul" => "Có ai nghe thấy ta không? …Cái bóng không đáp. Ta không biết nó còn nhớ đường ra hay không.",
            "brazier" => "Lửa vẫn còn nóng. Có thể lấy chỗ này làm mốc, nhưng đứng mãi ở đây cũng không tìm được lối ra.",
            "mushroom" => "Nấm mọc lên từ những khe đá ẩm. Không biết có độc không… Tốt nhất đừng chạm vào.",
            "crystal" => level == 6 ? "Tinh thể lạnh buốt, ánh sáng hắt lên cả lớp băng. Chúng không chỉ đường cho ta."
                : "Những cạnh tinh thể bắt ánh sáng. Thoạt nhìn cứ tưởng có một lối mở bên kia.",
            "candle" => "Sáp chảy thành từng lớp. Đã có người thắp nến ở đây… để cầu nguyện hay để chờ đợi?",
            "statue" => level == 10 ? kingDead
                ? "Tượng vẫn đứng chầu hai bên điện. Kẻ được tôn kính ở đây đã gục, chỉ còn những khối đá im lặng."
                : "Tượng đứng chầu hai bên điện. Cả nơi này được dựng để tôn kính kẻ trên ngai."
                : "Tượng đứng bất động như một lính canh. Ta vẫn nên nhìn kỹ trước khi đi qua.",
            "pillar" => knowsInscription
                ? "Những nét khắc chạy dọc thân cột. Không giống lời nhắn trên bia… Ta chưa hiểu được chúng."
                : "Những nét khắc chạy dọc thân cột. Là chữ hay hoa văn? Ta chưa hiểu được chúng.",
            "inlay" => "Hoa văn đã mòn nhưng vẫn nhận ra được. Có thể nhớ dấu này để biết mình đã đi qua phòng nào.",
            "stairs" => "Những bậc đá đã mòn. Có dấu người qua lại, nhưng không thấy một lối ra ở đây.",
            "plaque" => "Mặt phù điêu bị bụi che gần hết. Những đường nét còn lại không đủ để đọc thành lời.",
            "storage" => "Chỉ còn gỗ mục và bụi. Không có thứ gì dùng được trong này.",
            "pottery" => "Miệng bình sứt rồi. Bên trong chỉ còn cặn khô, không có nước để mang theo.",
            "relic" => level == 8 ? solvedDais
                ? "Di vật vẫn nằm yên giữa bụi. Lời giải trên bệ đã rõ rồi; ta cần đi tìm Vua Xương."
                : knowsDais ? "Đồ vật này nằm giữa những dấu vết cũ. Nếu có lời giải, ta vẫn phải tìm trên bệ khắc chữ."
                : "Một di vật giữa những dấu vết cũ. Có lẽ quanh đây vẫn còn chữ nào đó đọc được."
                : "Một di vật được đặt riêng ở đây. Có lẽ từng rất quan trọng với người sống trong nơi này.",
            _ => "Đồ vật phủ đầy bụi. Ta xem kỹ rồi… Không có gì có thể mang theo hoặc mở đường."
        };
    }
}
