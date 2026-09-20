using UnityEngine;

public class PlayerControlsDebugView : MonoBehaviour
{
    [SerializeField] private bool _showDebug = true;

    [Header("Layout")]
    [SerializeField] private Vector2 _margin = new(20f, 0f);
    [SerializeField, Min(150f)] private float _width = 220f;
    [SerializeField, Min(16f)] private float _lineHeight = 22f;

    private void OnGUI()
    {
        if (!_showDebug)
            return;

        float height = 45f + _lineHeight * 15f;
        float x = _margin.x;
        float y = (Screen.height - height) / 2f + _margin.y;
        float lineY = y + 25f;

        GUI.Box(new Rect(x, y, _width, height), "Player Debug");

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "Controls / input");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "  [1] CancelAction");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "-Block");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "  [RMB] Block");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "-Attack");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [LMB] Attack");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "-Spell");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [Tab] Spell");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "  [E] PassAction");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "-Gun");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [Q] Gun");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [LMB] Shoot");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [RMB] Aim");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [E] Miss");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [R] Reload");
        lineY += _lineHeight;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), " [Tab] Spell");
    }
}
