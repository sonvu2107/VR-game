using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class KeyPickup : MonoBehaviour
{
    private GameManager gameManager;
    private bool collected = false;

    private void Awake()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
    }

    private void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
        {
            collected = true;
            if (gameManager != null)
            {
                gameManager.KeyCollected();
            }
            Destroy(gameObject);
        }
    }
}
