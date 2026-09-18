using UnityEngine;

[ExecuteAlways]
public sealed class RadialPushEffectDebug : MonoBehaviour
{
    [SerializeField] private EffectDefinition effectDefinition;
    [SerializeField] private Transform effectOrigin;
    [SerializeField] private Color radiusColor = new Color(1f, 0.65f, 0f, 0.9f);
    [SerializeField] private bool drawWhenNotSelected = true;

    private void OnDrawGizmos()
    {
        if (!drawWhenNotSelected)
        {
            return;
        }

        DrawRadius();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawWhenNotSelected)
        {
            return;
        }

        DrawRadius();
    }

    private void DrawRadius()
    {
        if (effectDefinition == null ||
            effectDefinition.Type != EffectType.RadialPush)
        {
            return;
        }

        Transform origin = effectOrigin != null ? effectOrigin : transform;
        Gizmos.color = radiusColor;
        Gizmos.DrawWireSphere(origin.position, effectDefinition.Radius);
        Gizmos.DrawSphere(origin.position, 0.12f);
    }
}
