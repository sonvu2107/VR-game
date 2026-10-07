using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerupCircleController : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public SpriteRenderer circleSpriteRenderer;
    public int numDivisions = 50;
    public float radius = 0.5f;
    [SerializeField] private bool showLegacyChargeCircle;

    void Start()
    {
        lineRenderer.widthMultiplier = 0.1f;
        circleSpriteRenderer.color = new Color(1, 1, 1, 0.1f);
        UpdateVisibility(false);
    }

    void Update()
    {
        bool shouldShow = showLegacyChargeCircle && radius > 0.5f;
        UpdateVisibility(shouldShow);
        if (!shouldShow)
            return;

        lineRenderer.positionCount = numDivisions+1;
        for (int i = 0; i < numDivisions + 1; i++)
        {
            float angle = i * Mathf.PI * 2 / numDivisions;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
        circleSpriteRenderer.transform.localScale = new Vector3(2*radius, 2*radius, 1);
    }

    public void setRadius(float radius)
    {
        this.radius = radius;
    }

    private void UpdateVisibility(bool visible)
    {
        lineRenderer.enabled = visible;
        circleSpriteRenderer.enabled = visible;
    }
}
