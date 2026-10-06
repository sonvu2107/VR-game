using UnityEngine;

/// <summary>
/// Runtime singleton that stores all player stats.
/// Values persist across floor transitions because the instance lives
/// on a DontDestroyOnLoad object managed by PlayerMovement.
/// Reset happens only when the dungeon run restarts (new scene load).
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Player/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("Base Values (set in the asset)")]
    public float baseSpeed = 5f;
    public int baseAttackDamage = 1;
    public int basePowerUpDamage = 3;
    public float baseAttackRange = 0.5f;
    public float basePowerUpCooldown = 15f;
    [Tooltip("Số mạng ban đầu (hiển thị bằng trái tim). Giữ nguyên qua tất cả level.")]
    public int baseLives = 5;
    [Tooltip("Máu tối đa của nhân vật (thanh HP trên đầu).")]
    public float baseMaxHP = 100f;
    public float baseDashSpeed = 18f;
    public float baseDashDuration = 0.15f;
    public float baseDashCooldown = 1.5f;
    public float baseMaxStamina = 100f;
    public float baseDashStaminaCost = 25f;
    public float baseStaminaRegenRate = 20f;

    [Header("Runtime Bonuses (accumulated from items)")]
    [HideInInspector] public float bonusSpeed;
    [HideInInspector] public int bonusDamage;
    [HideInInspector] public int bonusPowerUpDamage;
    [HideInInspector] public float bonusAttackRange;
    [HideInInspector] public float bonusCooldownReduction;
    [HideInInspector] public float bonusMaxHP;
    [HideInInspector] public float bonusMaxStamina;
    [HideInInspector] public float bonusDashSpeedMult;
    [HideInInspector] public int bonusArmor;
    [HideInInspector] public float bonusBlockChance;

    [Header("Runtime State (persists across floors)")]
    [HideInInspector] public int currentLives = -1;  // -1 = not initialized
    [HideInInspector] public int gold = 0;

    // --- Computed getters ---------------------------------------------------
    public float Speed => baseSpeed + bonusSpeed;
    public int AttackDamage => baseAttackDamage + bonusDamage;
    public int PowerUpDamage => basePowerUpDamage + bonusPowerUpDamage;
    public float AttackRange => baseAttackRange + bonusAttackRange;
    public float PowerUpCooldown => Mathf.Max(3f, basePowerUpCooldown - bonusCooldownReduction);
    public int Lives => currentLives < 0 ? baseLives : currentLives;
    public float MaxHP => baseMaxHP + bonusMaxHP;
    public float DashSpeed => baseDashSpeed * (1f + bonusDashSpeedMult);
    public float DashDuration => baseDashDuration;
    public float DashCooldown => Mathf.Max(0.3f, baseDashCooldown - bonusCooldownReduction * 0.1f);
    public float MaxStamina => baseMaxStamina + bonusMaxStamina;
    public float DashStaminaCost => baseDashStaminaCost;
    public float StaminaRegenRate => baseStaminaRegenRate;
    public int Armor => bonusArmor;
    public float BlockChance => bonusBlockChance;
    public int Gold => gold;

    /// <summary>
    /// Called at the start of a new dungeon run to zero out all bonuses.
    /// </summary>
    public void ResetBonuses()
    {
        bonusSpeed = 0f;
        bonusDamage = 0;
        bonusPowerUpDamage = 0;
        bonusAttackRange = 0f;
        bonusCooldownReduction = 0f;
        bonusMaxHP = 0f;
        bonusMaxStamina = 0f;
        bonusDashSpeedMult = 0f;
        bonusArmor = 0;
        bonusBlockChance = 0f;
        currentLives = baseLives;  // Reset lives to base
        gold = 0;
    }

    /// <summary>Add gold to the player's wallet.</summary>
    public void AddGold(int amount)
    {
        gold += amount;
    }

    /// <summary>Lose one life. Returns true if still alive.</summary>
    public bool LoseLife()
    {
        if (currentLives < 0) currentLives = baseLives;
        currentLives--;
        return currentLives > 0;
    }

    /// <summary>Apply an item's upgrades.</summary>
    public void ApplyUpgrade(ItemDataSO item)
    {
        if (item == null) return;

        bonusSpeed += item.speedBonus;
        bonusDamage += item.damageBonus;
        bonusPowerUpDamage += item.powerUpDamageBonus;
        bonusAttackRange += item.attackRangeBonus;
        bonusCooldownReduction += item.cooldownReduction;
        bonusMaxHP += item.maxHPBonus;
        bonusMaxStamina += item.staminaBonus;
        bonusDashSpeedMult += item.dashSpeedMultBonus;
        bonusArmor += item.armorBonus;
        bonusBlockChance += item.blockChanceBonus;
    }
}
