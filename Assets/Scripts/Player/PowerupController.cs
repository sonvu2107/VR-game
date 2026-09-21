using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerupController : MonoBehaviour
{
    public Image coolDownImage;
    public Sprite[] coolDownSprites;
    public TextMeshProUGUI coolDownText;

    [Min(0.01f)] public float cooldownDuration = 15.0f;

    private void Start()
    {
        ShowPoweredUp();
    }

    public void UpdateCooldown(float cooldown)
    {
        if (coolDownImage != null && coolDownSprites != null && coolDownSprites.Length > 0)
        {
            float normalizedCooldown = Mathf.Clamp01(cooldown / cooldownDuration);
            int spriteIndex = Mathf.Clamp(
                Mathf.RoundToInt(normalizedCooldown * (coolDownSprites.Length - 1)),
                0,
                coolDownSprites.Length - 1);
            coolDownImage.sprite = coolDownSprites[spriteIndex];
        }

        if (coolDownText != null)
        {
            coolDownText.enabled = true;
            coolDownText.text = Mathf.CeilToInt(Mathf.Max(0f, cooldown)).ToString();
        }
    }

    public void ShowPoweredUp()
    {
        if (coolDownText != null)
        {
            coolDownText.enabled = true;
            coolDownText.text = "READY";
        }

        if (coolDownImage != null && coolDownSprites != null && coolDownSprites.Length > 0)
            coolDownImage.sprite = coolDownSprites[0];
    }

    public void HidePoweredUp()
    {
        if (coolDownText != null)
            coolDownText.enabled = false;
    }
}
