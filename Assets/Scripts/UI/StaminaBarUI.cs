using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple HUD element that shows the player's stamina as a fill bar
/// and the dash cooldown state. Attach to a UI Canvas.
/// </summary>
public class StaminaBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The fill Image inside the stamina bar (Image Type = Filled).")]
    public Image fillImage;

    [Tooltip("Optional — Text showing stamina value.")]
    public TMPro.TextMeshProUGUI staminaText;

    [Tooltip("Optional — Image that greys out while dash is on cooldown.")]
    public Image dashCooldownOverlay;

    [Header("Colors")]
    public Color fullColor = new Color(0.2f, 0.85f, 0.4f, 1f);
    public Color lowColor = new Color(0.9f, 0.25f, 0.2f, 1f);

    private PlayerMovement player;

    private void Start()
    {
        player = FindObjectOfType<PlayerMovement>();

        // Tự động tạo ảnh trắng và gán vào nếu bạn chưa có ảnh
        if (fillImage != null && fillImage.sprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            fillImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
        }
    }

    private void Update()
    {
        if (player == null || player.playerStats == null) return;

        float max = player.playerStats.MaxStamina;
        float cur = player.CurrentStamina;
        float ratio = Mathf.Clamp01(cur / max);

        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
            fillImage.color = Color.Lerp(lowColor, fullColor, ratio);
        }

        if (staminaText != null)
            staminaText.text = $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";

        if (dashCooldownOverlay != null)
            dashCooldownOverlay.gameObject.SetActive(player.DashCooldownRemaining > 0f);
    }
}
