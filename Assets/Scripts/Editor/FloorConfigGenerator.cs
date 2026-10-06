using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor-only script. Automatically creates 10 FloorConfig assets
/// with pre-configured settings matching the 10-level design document.
/// Run from menu: Tools → Generate Floor Configs
/// </summary>
public class FloorConfigGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Floor Configs")]
    public static void Generate()
    {
        string folder = "Assets/FloorConfigs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "FloorConfigs");

        CreateFloor(folder, 1, "Khởi đầu",
            "Bản đồ nhỏ, ít Skeleton. Làm quen di chuyển và chiến đấu.",
            rooms: 6, corridor: 15, minE: 1, maxE: 3);

        CreateFloor(folder, 2, "Mở rộng",
            "Nhiều phòng và quái hơn. Tiêu diệt toàn bộ enemy.",
            rooms: 10, corridor: 20, minE: 3, maxE: 5);

        CreateFloor(folder, 3, "Tấn công từ xa",
            "Thêm enemy tầm xa. Tìm và tiêu diệt đội hình hỗn hợp.",
            rooms: 10, corridor: 20, minE: 2, maxE: 4);

        CreateFloor(folder, 4, "Cạm bẫy",
            "Bẫy gai, hành lang hẹp. Tìm chìa khóa để mở cổng.",
            rooms: 10, corridor: 25, minE: 2, maxE: 4,
            hasTraps: true, trapsPerRoom: 3,
            requireKeys: true, keysRequired: 2);

        CreateFloor(folder, 5, "Mini-Boss I",
            "Phòng đấu trường. Đánh bại mini-boss đầu tiên.",
            rooms: 8, corridor: 20, minE: 2, maxE: 4,
            hasBoss: true);

        CreateFloor(folder, 6, "Bóng tối",
            "Ánh sáng hạn chế. Tìm các trụ phóng ẩn.",
            rooms: 10, corridor: 20, minE: 3, maxE: 5,
            darkFloor: true, lightRadius: 5f, wallDecoChance: 0.15f);

        CreateFloor(folder, 7, "Elite",
            "Quái tinh anh có chỉ số cao. Sống sót qua nhiều đợt tấn công.",
            rooms: 10, corridor: 20, minE: 3, maxE: 6,
            waveMode: true, waves: 3);

        CreateFloor(folder, 8, "Mê cung khóa",
            "Nhiều nhánh và phòng bí mật. Thu thập đủ chìa khóa.",
            rooms: 14, corridor: 30, minE: 2, maxE: 4,
            hasTraps: true, trapsPerRoom: 2,
            requireKeys: true, keysRequired: 4);

        CreateFloor(folder, 9, "Thử thách cuối",
            "Quái hỗn hợp và mini-boss. Hoàn thành chuỗi phòng chiến đấu.",
            rooms: 12, corridor: 20, minE: 4, maxE: 7,
            hasBoss: true, hasTraps: true, trapsPerRoom: 2);

        CreateFloor(folder, 10, "Boss Cuối",
            "Đấu trường boss riêng. Đánh bại boss nhiều giai đoạn.",
            rooms: 6, corridor: 15, minE: 2, maxE: 3,
            hasBoss: true, darkFloor: true, lightRadius: 7f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("✅ 10 FloorConfig assets created in Assets/FloorConfigs/");
        EditorUtility.DisplayDialog("Done!", "10 FloorConfig đã được tạo trong Assets/FloorConfigs/", "OK");
    }

    private static void CreateFloor(string folder, int num, string floorName, string desc,
        int rooms = 10, int corridor = 20, int minE = 2, int maxE = 5,
        bool hasBoss = false, bool hasTraps = false, int trapsPerRoom = 0,
        bool requireKeys = false, int keysRequired = 0,
        bool darkFloor = false, float lightRadius = 8f,
        bool waveMode = false, int waves = 1, float wallDecoChance = 0.08f)
    {
        string path = $"{folder}/Floor_{num:D2}.asset";

        // Don't overwrite if already exists
        if (AssetDatabase.LoadAssetAtPath<FloorConfigSO>(path) != null)
        {
            Debug.Log($"Floor_{num:D2} already exists, skipping.");
            return;
        }

        FloorConfigSO config = ScriptableObject.CreateInstance<FloorConfigSO>();
        config.floorName = floorName;
        config.description = desc;
        config.numberOfRooms = rooms;
        config.corridorLength = corridor;
        config.minEnemiesPerRoom = minE;
        config.maxEnemiesPerRoom = maxE;
        config.hasBoss = hasBoss;
        config.hasTraps = hasTraps;
        config.trapsPerRoom = trapsPerRoom;
        config.requireKeys = requireKeys;
        config.keysRequired = keysRequired;
        config.darkFloor = darkFloor;
        config.lightRadius = lightRadius;
        config.waveMode = waveMode;
        config.numberOfWaves = waves;
        config.wallDecoChance = wallDecoChance;

        AssetDatabase.CreateAsset(config, path);
        Debug.Log($"Created {path}");
    }
}
