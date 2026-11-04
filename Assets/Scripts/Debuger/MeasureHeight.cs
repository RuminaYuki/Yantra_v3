using UnityEngine;

public class MeasureHeight : MonoBehaviour
{
    public string height;
    public float targetHeightM;
    public bool debugConsole;

    [ContextMenu("Log Height")]
    string Log()
    {
        if (!TryGetBounds(out Bounds total)) { Debug.LogWarning("No renderer"); return "No renderer"; }

        if(debugConsole)
            Debug.Log($"<b>Total Height: {total.size.y:F2} m</b>");
        return $"Total Height: {total.size.y:F2} m";
    }

    [ContextMenu("Apply Height")]
    void ApplyHeight()
    {
        if (targetHeightM <= 0f) { Debug.LogWarning("Target height must be greater than 0"); return; }
        if (!TryGetBounds(out Bounds total)) { Debug.LogWarning("No renderer"); return; }
        if (total.size.y <= 0f) { Debug.LogWarning("Current height is 0"); return; }

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(transform, "Apply Height");
#endif
        float factor = targetHeightM / total.size.y; // เทียบกับความสูงปัจจุบัน
        transform.localScale *= factor;

        height = Log();
    }

    bool TryGetBounds(out Bounds total)
    {
        total = default;

        var renderers = GetComponentsInChildren<Renderer>(); // เอาทุกตัว (มี s)
        if (renderers.Length == 0) return false;

        total = renderers[0].bounds;
        foreach (var r in renderers)
        {
            total.Encapsulate(r.bounds);              // ขยายกล่องให้ครอบชิ้นนี้ด้วย
            if(debugConsole)
                Debug.Log($"{r.name}: {r.bounds.size.y:F2} m");
        }
        return true;
    }

    void OnEnable()
    {
        height = Log();
    }
    void OnDisable()
    {
        height = "";
    }
}
