using System.Drawing;
using UnityEngine;

public class StrokeDrawer : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;

    public void DrawStroke(Vector3 position)
    {
        if (lineRenderer == null) return;
        lineRenderer.positionCount++;
        lineRenderer.SetPosition(lineRenderer.positionCount - 1, position);
        Debug.Log($"Stroke drawn at position: {position}");
    }
}
