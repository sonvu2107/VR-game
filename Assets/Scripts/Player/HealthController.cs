using UnityEngine;
using UnityEngine.UI;

public class HealthController : MonoBehaviour
{
    [SerializeField] private int maxHeartAmount = 10;
    public int startHeart = 3;
    public int currentHealth;
    [SerializeField] private int healthPerHeart = 2;
    private int maxHealth;

    public GameManager gameManager;
    private Animator animator;
    
    public Image[] heartImages;
    public Sprite[] heartSprites;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        startHeart = Mathf.Clamp(startHeart, 0, Mathf.Min(maxHeartAmount, heartImages.Length));
        maxHealth = maxHeartAmount * healthPerHeart;
        currentHealth = startHeart * healthPerHeart;
        CheckHealthAmount();
    }
    
    private void CheckHealthAmount()
    {
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] != null)
                heartImages[i].enabled = i < startHeart;
        }
        
        UpdateHearts();
    }

    private void UpdateHearts()
    {
        if (heartSprites == null || heartSprites.Length == 0 || healthPerHeart <= 0)
            return;

        bool empty = false;
        int i = 0;

        foreach (Image image in heartImages)
        {
            if (image == null)
                continue;

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
                    int currentHeartHealth = Mathf.Max(0, currentHealth - healthPerHeart * (i - 1));
                    float normalizedHealth = (float)currentHeartHealth / healthPerHeart;
                    int imageIndex = Mathf.Clamp(
                        Mathf.CeilToInt(normalizedHealth * (heartSprites.Length - 1)),
                        0,
                        heartSprites.Length - 1);
                    image.sprite = heartSprites[imageIndex];
                    empty = true;
                }
            }
        }
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (animator != null)
            animator.SetTrigger("Hurt");

        UpdateHearts();

        if (currentHealth <= 0)
        {
            if (animator != null)
                animator.SetBool("isDead", true);

            if (gameManager != null)
                gameManager.GameOver();
        }
    }

    public void AddHeartContainer()
    {
        startHeart++;
        startHeart = Mathf.Clamp(startHeart, 0, maxHeartAmount);
        
        currentHealth = startHeart * healthPerHeart;
        maxHealth = maxHeartAmount * healthPerHeart;
        
        CheckHealthAmount();
    }
}
