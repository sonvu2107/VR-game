using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class FloorExit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Color lockedColor = new(0.65f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color unlockedColor = new(0.15f, 0.85f, 0.85f, 0.95f);

    private GameManager gameManager;

    public bool IsUnlocked { get; private set; }

    private void Awake()
    {
        Collider2D exitCollider = GetComponent<Collider2D>();
        exitCollider.isTrigger = true;

        if (visual == null)
            visual = GetComponent<SpriteRenderer>();

        SetUnlocked(false);
    }

    public void Initialize(GameManager manager)
    {
        gameManager = manager;
        SetUnlocked(false);
    }

    public void SetUnlocked(bool unlocked)
    {
        IsUnlocked = unlocked;

        if (visual != null)
            visual.color = unlocked ? unlockedColor : lockedColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsUnlocked || other.GetComponentInParent<PlayerMovement>() == null)
            return;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        gameManager?.TryAdvanceFloor();
    }
}
