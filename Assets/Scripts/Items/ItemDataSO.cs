using UnityEngine;

/// <summary>
/// Data definition for a pickup item.
/// Create assets via Assets → Create → Items → ItemData.
/// </summary>
[CreateAssetMenu(fileName = "Item_", menuName = "Items/ItemData")]
public class ItemDataSO : ScriptableObject
{
    [Header("Display")]
    public string itemName = "New Item";
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public Color glowColor = Color.white;

    [Header("Stat Bonuses")]
    [Tooltip("Flat speed bonus added to base speed.")]
    public float speedBonus;

    [Tooltip("Flat attack damage bonus.")]
    public int damageBonus;

    [Tooltip("Flat bonus to power-up (charged) attack damage.")]
    public int powerUpDamageBonus;

    [Tooltip("Flat bonus to melee attack range.")]
    public float attackRangeBonus;

    [Tooltip("Reduces cooldown timers (power-up and dash). Clamped to a minimum.")]
    public float cooldownReduction;

    [Tooltip("Flat bonus to max HP.")]
    public float maxHPBonus;

    [Tooltip("Flat bonus to max stamina.")]
    public float staminaBonus;

    [Tooltip("Percentage bonus to dash speed (0.1 = +10%).")]
    public float dashSpeedMultBonus;

    [Header("Defense Bonuses (Shields/Armor)")]
    [Tooltip("Flat damage reduction. Incoming damage is reduced by this amount (minimum 1 damage taken).")]
    public int armorBonus;

    [Tooltip("Percentage chance to completely block/dodge an attack (e.g. 0.2 = 20%).")]
    [Range(0f, 1f)] public float blockChanceBonus;

    [Header("Instant Effects")]
    [Tooltip("Instantly restore this much HP on pickup (absolute value).")]
    public int instantHeal;

    [Tooltip("Heal a percentage of max HP (0.35 = 35%). Stacks with instantHeal.")]
    [Range(0f, 1f)] public float healPercent;
}
