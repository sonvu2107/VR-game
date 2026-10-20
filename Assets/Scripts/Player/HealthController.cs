using UnityEngine;
using UnityEngine.UI;

public class HealthController : MonoBehaviour
{
    private int maxHeartAmount = 10;
    public int startHeart = 5;
    public int currentHealth;
    private int maxHealth;
    private int healthPerHeart = 2;
    public int MaxHealth => startHeart * healthPerHeart;

    public GameManager gameManager;
    private Animator animator;
    
    public Image[] heartImages;
    public Sprite[] heartSprites;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        startHeart = Mathf.Clamp(startHeart, 1, maxHeartAmount);
        maxHealth = MaxHealth;
        currentHealth = maxHealth;
        checkHealthAmount();
    }
    
    void checkHealthAmount()
    {
        if (GetComponent<LanPlayerInput>() != null || heartImages == null) return;
        for (int i=0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;
            if (startHeart <= i)
            {
                heartImages[i].enabled = false;
            }
            else
            {
                heartImages[i].enabled = true;
            }
        }
        
        UpdateHearts();
    }

    void UpdateHearts()
    {
        if (GetComponent<LanPlayerInput>() != null || heartImages == null || heartSprites == null || heartSprites.Length < 2) return;
        bool empty = false;
        int i = 0;

        foreach (Image image in heartImages)
        {
            if (image == null) continue;
            if (empty)
            {
                image.sprite = heartSprites[0];
            }
            else
            {
                i++;
                if (currentHealth >= i * healthPerHeart)
                {
                    image.sprite = heartSprites[^1];
                }
                else
                {
                    int currentHeartHealth = (int)(healthPerHeart - (healthPerHeart * i - currentHealth));
                    int imageIndex = Mathf.Clamp(Mathf.RoundToInt(
                        (float)currentHeartHealth / healthPerHeart * (heartSprites.Length - 1)),
                        0, heartSprites.Length - 1);
                    image.sprite = heartSprites[imageIndex];
                    empty = true;
                }
            }
        }
    }

    [SerializeField] private float invulnerableDuration = 0.85f;
    [SerializeField] private int maxDamagePerHit = 1; // 1 máu tương đương nửa trái tim (healthPerHeart = 2)
    private float invulnerableUntil = -1f;
    public float LastDamageAt { get; private set; } = float.NegativeInfinity;
    private SpriteRenderer[] renderers;
    private Coroutine flashRoutine;

    public void TakeDamage(int amount)
    {
        if (LanLaunch.Mode == LanMode.Client || amount <= 0 || currentHealth <= 0) return;
        if (gameManager != null && gameManager.CurrentState == GameState.Story) return;
        if (Time.time < invulnerableUntil) return;

        if (maxDamagePerHit > 0)
        {
            amount = Mathf.Min(amount, maxDamagePerHit);
        }

        invulnerableUntil = Time.time + invulnerableDuration;
        LastDamageAt = Time.time;
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (animator != null) animator.SetTrigger("Hurt");
        UpdateHearts();

        if (currentHealth <= 0)
        {
            if (animator != null) animator.SetBool("isDead", true);
            if (LanSessionBridge.Active != null && LanSessionBridge.Active.HasGuest)
                LanSessionBridge.Active.PlayerDied(this);
            else if (gameManager != null)
                gameManager.GameOver();
        }
        else
        {
            StartInvulnerabilityFlash();
        }
    }

    private void StartInvulnerabilityFlash()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(InvulnerabilityFlashRoutine());
    }

    private System.Collections.IEnumerator InvulnerabilityFlashRoutine()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>();

        float elapsed = 0f;
        bool visible = true;
        while (elapsed < invulnerableDuration)
        {
            visible = !visible;
            float alpha = visible ? 1f : 0.35f;
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    Color c = r.color;
                    c.a = alpha;
                    r.color = c;
                }
            }
            yield return new WaitForSeconds(0.08f);
            elapsed += 0.08f;
        }

        // Restore full opacity
        foreach (var r in renderers)
        {
            if (r != null)
            {
                Color c = r.color;
                c.a = 1f;
                r.color = c;
            }
        }
        flashRoutine = null;
    }

    public bool TryHeal(int amount)
    {
        if (LanLaunch.Mode == LanMode.Client || amount <= 0 || currentHealth <= 0 || currentHealth >= MaxHealth)
            return false;

        currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        UpdateHearts();
        return true;
    }

    public void RestoreFullHealth()
    {
        ResetDamageFeedback();
        maxHealth = MaxHealth;
        currentHealth = maxHealth;
        checkHealthAmount();
    }

    public void AddHeartContainer()
    {
        startHeart++;
        startHeart = Mathf.Clamp(startHeart, 0, maxHeartAmount);
        
        currentHealth = startHeart * healthPerHeart;
        maxHealth = MaxHealth;
        
        checkHealthAmount();
    }

    public void ResetForLanRun()
    {
        ResetDamageFeedback();
        currentHealth = startHeart * healthPerHeart;
        maxHealth = MaxHealth;
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null) animator.SetBool("isDead", false);
        UpdateHearts();
    }

    public void ApplyLanHealth(int health, int hearts)
    {
        currentHealth = health;
        startHeart = hearts;
        maxHealth = MaxHealth;
        checkHealthAmount();
    }

    private void ResetDamageFeedback()
    {
        invulnerableUntil = -1f;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = null;
        if (renderers == null) return;
        foreach (SpriteRenderer renderer in renderers)
            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = 1f;
                renderer.color = color;
            }
    }
}
