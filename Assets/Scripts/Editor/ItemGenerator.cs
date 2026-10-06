using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool: tự động tạo 8 vật phẩm (ItemDataSO) với icon từ asset Tiny Swords.
/// Menu: Tools → Generate Items
/// </summary>
public class ItemGenerator
{
    private const string IconPath = "Assets/Imports/Tiny Swords (Update 010)/UI/Icons/";
    private const string ResourcePath = "Assets/Imports/Tiny Swords (Update 010)/Resources/Resources/";
    private const string OutputPath = "Assets/ScriptableObjects/Items/";

    [MenuItem("Tools/Generate Items")]
    public static void GenerateAllItems()
    {
        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Items"))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Items");

        // ── 1. Iron Shield (Khiên Sắt) ── Icon 01 (hình chữ X / chắn)
        CreateItem("Item_IronShield", "Iron Shield", "Tấm khiên rắn chắc giảm sát thương nhận vào.",
            IconPath + "Regular_01.png", new Color(0.6f, 0.8f, 1f),
            armorBonus: 2, blockChance: 0.15f, maxHPBonus: 20);

        // ── 2. Knight Armor (Giáp Hiệp Sĩ) ── Icon 08 (hình dấu +)
        CreateItem("Item_KnightArmor", "Knight Armor", "Bộ giáp nặng giảm mạnh sát thương nhưng hơi chậm.",
            IconPath + "Regular_08.png", new Color(0.8f, 0.8f, 0.9f),
            armorBonus: 4, maxHPBonus: 50, speedBonus: -0.5f);

        // ── 3. Dodge Cloak (Áo choàng né đòn) ── Icon 02 (hình hoa)
        CreateItem("Item_DodgeCloak", "Dodge Cloak", "Áo choàng ma thuật giúp né được 30% đòn đánh.",
            IconPath + "Regular_02.png", new Color(0.9f, 0.5f, 1f),
            blockChance: 0.30f, speedBonus: 1f);

        // ── 4. War Hammer (Búa chiến) ── Icon 04 (hình búa/kiếm)
        CreateItem("Item_WarHammer", "War Hammer", "Búa chiến nặng tăng sát thương chém rất mạnh.",
            IconPath + "Regular_04.png", new Color(1f, 0.5f, 0.3f),
            damageBonus: 3, powerUpDamageBonus: 5, attackRangeBonus: 0.15f);

        // ── 5. Swift Boots (Giày tốc biến) ── Icon 05 (hình chữ S)
        CreateItem("Item_SwiftBoots", "Swift Boots", "Giày phép thuật giúp di chuyển và lướt nhanh hơn.",
            IconPath + "Regular_05.png", new Color(0.3f, 1f, 0.7f),
            speedBonus: 2f, dashSpeedMult: 0.3f, cooldownReduction: 2f);

        // ── 6. Healing Meat (Thịt hồi máu) ── Meat resource icon
        CreateItem("Item_HealingMeat", "Healing Meat", "Miếng thịt nướng thơm phức hồi 40% HP tối đa.",
            ResourcePath + "M_Idle.png", new Color(1f, 0.4f, 0.3f),
            instantHeal: 10, healPercent: 0.40f);

        // ── 7. Gold Purse (Túi vàng) ── Gold resource icon
        CreateItem("Item_GoldPurse", "Gold Purse", "Túi vàng tràn đầy năng lượng, tăng mọi chỉ số nhẹ.",
            ResourcePath + "G_Idle.png", new Color(1f, 0.9f, 0.2f),
            damageBonus: 1, maxHPBonus: 15, speedBonus: 0.5f, armorBonus: 1);

        // ── 8. Ancient Scroll (Cuộn phép cổ) ── Icon 09 (hình cuộn giấy)
        CreateItem("Item_AncientScroll", "Ancient Scroll", "Cuộn phép cổ đại giảm thời gian hồi chiêu và tăng sức mạnh tuyệt chiêu.",
            IconPath + "Regular_09.png", new Color(0.4f, 0.6f, 1f),
            powerUpDamageBonus: 8, cooldownReduction: 4f, staminaBonus: 30f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Successfully generated 8 items in " + OutputPath);
    }

    private static void CreateItem(string fileName, string itemName, string description,
        string iconPath, Color glowColor,
        float speedBonus = 0, int damageBonus = 0, int powerUpDamageBonus = 0,
        float attackRangeBonus = 0, float cooldownReduction = 0,
        float maxHPBonus = 0, float staminaBonus = 0, float dashSpeedMult = 0,
        int armorBonus = 0, float blockChance = 0,
        int instantHeal = 0, float healPercent = 0)
    {
        string fullPath = OutputPath + fileName + ".asset";

        // Skip if already exists
        if (AssetDatabase.LoadAssetAtPath<ItemDataSO>(fullPath) != null)
        {
            Debug.Log($"Item '{fileName}' already exists, skipping.");
            return;
        }

        ItemDataSO item = ScriptableObject.CreateInstance<ItemDataSO>();
        item.itemName = itemName;
        item.description = description;
        item.glowColor = glowColor;

        // Load icon sprite
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (icon == null)
            Debug.LogWarning($"Could not load icon at: {iconPath}");
        item.icon = icon;

        // Stats
        item.speedBonus = speedBonus;
        item.damageBonus = damageBonus;
        item.powerUpDamageBonus = powerUpDamageBonus;
        item.attackRangeBonus = attackRangeBonus;
        item.cooldownReduction = cooldownReduction;
        item.maxHPBonus = maxHPBonus;
        item.staminaBonus = staminaBonus;
        item.dashSpeedMultBonus = dashSpeedMult;
        item.armorBonus = armorBonus;
        item.blockChanceBonus = blockChance;
        item.instantHeal = instantHeal;
        item.healPercent = healPercent;

        AssetDatabase.CreateAsset(item, fullPath);
        Debug.Log($"Created item: {itemName} → {fullPath}");
    }
}
