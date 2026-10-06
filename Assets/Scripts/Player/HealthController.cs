using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the player's HP (hit points) and Lives system.
/// - HP is shown as a floating health bar above the player's head.
/// - Lives (hearts) are shown in the HUD UI.
/// - When HP reaches 0 → lose 1 life, HP resets to full.
/// - When all lives are lost → Game Over.
/// </summary>
public class HealthController : MonoBehaviour
{
    [Header("Lives (Hearts UI)")]
    [Tooltip("Number of lives the player starts with.")]
    public int startLives = 5;
    private int maxDisplayLives = 10;

    public GameManager gameManager;
    public Image[] heartImages;
    public Sprite[] heartSprites; // Index 0 = empty, last = full

    [Header("HP Settings")]
    [Tooltip("Max HP if no PlayerStats assigned.")]
    public float fallbackMaxHP = 100f;

    private float maxHP;
    private float currentHP;
    private int currentLives;

    private Animator animator;
    private FloatingHealthBar healthBar;

    // Invulnerability after losing a life
    private float invulnTimer;
    private readonly float invulnDuration = 1.5f;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Read from PlayerStats if available
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null && pm.playerStats != null)
        {
            PlayerStats stats = pm.playerStats;
            maxHP = stats.MaxHP;
            currentLives = stats.Lives;
            startLives = stats.baseLives;
        }
        else
        {
            maxHP = fallbackMaxHP;
            currentLives = startLives;
        }

        currentHP = maxHP;

        // Create floating health bar
        healthBar = GetComponent<FloatingHealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<FloatingHealthBar>();

        UpdateLivesUI();
        UpdateHealthBar();
    }

    void Update()
    {
        if (invulnTimer > 0f)
            invulnTimer -= Time.deltaTime;
    }

    // ──────────────────── Lives UI (Hearts) ────────────────────
    void UpdateLivesUI()
    {
        if (heartImages == null) return;

        for (int i = 0; i < heartImages.Length && i < maxDisplayLives; i++)
        {
            if (i < currentLives)
            {
                heartImages[i].enabled = true;
                // Full heart sprite (last index)
                if (heartSprites != null && heartSprites.Length > 0)
                    heartImages[i].sprite = heartSprites[^1];
            }
            else if (i < startLives)
            {
                heartImages[i].enabled = true;
                // Empty heart sprite (first index)
                if (heartSprites != null && heartSprites.Length > 0)
                    heartImages[i].sprite = heartSprites[0];
            }
            else
            {
                heartImages[i].enabled = false;
            }
        }
    }

    // ──────────────────── Health Bar ────────────────────
    void UpdateHealthBar()
    {
        if (healthBar != null)
            healthBar.UpdateBar(currentHP / maxHP);
    }

    public void TakeDamage(int amount)
    {
        if (invulnTimer > 0f) return;

        // Fetch defensive stats if available
        int armor = 0;
        float blockChance = 0f;
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null && pm.playerStats != null)
        {
            armor = pm.playerStats.Armor;
            blockChance = pm.playerStats.BlockChance;
        }

        // Check for block/dodge
        if (blockChance > 0f && Random.value < blockChance)
        {
            // Successfully blocked/dodged!
            // Optional: You could spawn a "Blocked!" floating text here
            return;
        }

        // Apply armor damage reduction
        int actualDamage = Mathf.Max(1, amount - armor); // Luôn nhận ít nhất 1 sát thương nếu trúng

        currentHP -= actualDamage;
        currentHP = Mathf.Max(0, currentHP);

        animator.SetTrigger("Hurt");
        UpdateHealthBar();
        
        // --- Hiệu ứng tia máu và text báo sát thương ---
        SpawnBloodEffect(actualDamage);

        if (currentHP <= 0)
        {
            LoseLife();
        }
    }

    /// <summary>
    /// Chỉ hiện các dấu "-" nhỏ nổi lên khi bị thương.
    /// </summary>
    private void SpawnBloodEffect(int damage)
    {
        int dashCount = Mathf.Clamp(damage + 1, 2, 4);
        for (int i = 0; i < dashCount; i++)
        {
            GameObject dashObj = new GameObject("DmgDash");
            Vector3 offset = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 0.9f), 0f);
            dashObj.transform.position = transform.position + offset;

            var tmp = dashObj.AddComponent<TMPro.TextMeshPro>();
            tmp.text = "–";
            tmp.fontSize = Random.Range(1.6f, 2.4f);
            tmp.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.sortingOrder = 100;

            dashObj.AddComponent<TinyDashFloater>();
            Destroy(dashObj, 0.7f);
        }
    }

    void LoseLife()
    {
        // Consume a life from PlayerStats
        PlayerMovement pm = GetComponent<PlayerMovement>();
        bool stillAlive = false;

        if (pm != null && pm.playerStats != null)
        {
            stillAlive = pm.playerStats.LoseLife();
            currentLives = pm.playerStats.Lives;
        }
        else
        {
            currentLives--;
            stillAlive = currentLives > 0;
        }

        UpdateLivesUI();

        if (!stillAlive)
        {
            // Game Over
            animator.SetBool("isDead", true);
            gameManager.GameOver();
        }
        else
        {
            // Respawn: reset HP, brief invulnerability
            currentHP = maxHP;
            invulnTimer = invulnDuration;
            UpdateHealthBar();
        }
    }

    // ──────────────────── Healing ────────────────────
    /// <summary>Heal by an absolute amount.</summary>
    public void Heal(int amount)
    {
        currentHP += amount;
        currentHP = Mathf.Min(currentHP, maxHP);
        UpdateHealthBar();
    }

    /// <summary>Heal by a percentage of max HP (0.35 = 35%).</summary>
    public void HealPercent(float percent)
    {
        float healAmount = maxHP * percent;
        currentHP += healAmount;
        currentHP = Mathf.Min(currentHP, maxHP);
        UpdateHealthBar();
    }

    /// <summary>Increase max HP (from item upgrades).</summary>
    public void IncreaseMaxHP(float bonus)
    {
        maxHP += bonus;
        currentHP += bonus; // Also heal by the bonus amount
        UpdateHealthBar();
    }

    // ──────────────────── Legacy compatibility ────────────────────
    /// <summary>Add a heart container (extra life).</summary>
    public void AddHeartContainer()
    {
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null && pm.playerStats != null)
        {
            pm.playerStats.currentLives++;
            currentLives = pm.playerStats.Lives;
        }
        else
        {
            currentLives++;
        }

        currentLives = Mathf.Clamp(currentLives, 0, maxDisplayLives);
        UpdateLivesUI();
    }

    // ──────────────────── Public Getters ────────────────────
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public int CurrentLives => currentLives;
}
