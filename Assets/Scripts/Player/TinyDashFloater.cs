using UnityEngine;
using TMPro;

/// <summary>
/// Animates a tiny dash mark: floats upward and fades out gracefully.
/// Used for the damage indicator effect on player hit.
/// </summary>
public class TinyDashFloater : MonoBehaviour
{
    private TextMeshPro tmp;
    private float lifetime = 0.8f;
    private float elapsed = 0f;
    private float floatSpeed;
    private float driftX;
    private Color originalColor;

    private void Start()
    {
        tmp = GetComponent<TextMeshPro>();
        if (tmp != null)
            originalColor = tmp.color;

        // Mỗi dấu nổi với tốc độ và hướng hơi khác nhau
        floatSpeed = Random.Range(0.8f, 1.6f);
        driftX = Random.Range(-0.15f, 0.15f); // Tản ra hai bên nhẹ nhàng
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / lifetime; // 0 → 1

        // Nổi lên
        transform.position += new Vector3(driftX, floatSpeed, 0f) * Time.deltaTime;

        // Mờ dần theo đường cong — rõ lúc đầu, tan nhanh lúc cuối
        if (tmp != null)
        {
            float alpha = Mathf.Pow(1f - t, 2f); // easing out
            tmp.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
        }
    }
}
