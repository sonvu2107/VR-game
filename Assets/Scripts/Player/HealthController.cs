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
        for (int i=0; heartImages != null && i < heartImages.Length; i++)
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
        bool empty = false;
        int i = 0;

        if (heartImages == null || heartSprites == null || heartSprites.Length < 2)
            return;

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

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHealth <= 0) return;
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        animator.SetTrigger("Hurt");
        UpdateHearts();

        if (currentHealth <= 0)
        {
            animator.SetBool("isDead", true);
            gameManager.GameOver();
        }
    }

    public bool TryHeal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= MaxHealth)
            return false;

        currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        UpdateHearts();
        return true;
    }

    public void RestoreFullHealth()
    {
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
}
