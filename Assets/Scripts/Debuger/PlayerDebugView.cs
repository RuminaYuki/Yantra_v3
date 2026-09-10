using UnityEngine;

public class PlayerDebugView : MonoBehaviour
{
    private enum ScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [SerializeField] private Health _health;
    [SerializeField] private BaseLocomotion _locomotion;
    [SerializeField] private BlockSystem _blockSystem;
    [SerializeField] private SkillPoints _skillPoints;
    [SerializeField] private bool _showDebug = true;
    [SerializeField, Min(0.1f)] private float _cheatHealthAmount = 10f;
    [SerializeField] private KeyCode _decreaseHealthKey = KeyCode.Minus;
    [SerializeField] private KeyCode _increaseHealthKey = KeyCode.Equals;

    [Header("Layout")]
    [SerializeField] private ScreenCorner _screenCorner = ScreenCorner.TopRight;
    [SerializeField] private Vector2 _margin = new(20f, 20f);
    [SerializeField, Min(150f)] private float _width = 260f;
    [SerializeField, Min(16f)] private float _lineHeight = 22f;

    private void Awake()
    {
        if (_health == null)
            _health = GetComponent<Health>();
        if (_locomotion == null)
            _locomotion = GetComponent<BaseLocomotion>();
        if (_blockSystem == null)
            _blockSystem = GetComponent<BlockSystem>();
        if (_skillPoints == null)
            _skillPoints = GetComponent<SkillPoints>();
    }

    private void Update()
    {
        if (!_showDebug || _health == null)
            return;

        if (Input.GetKeyDown(_decreaseHealthKey) || Input.GetKeyDown(KeyCode.KeypadMinus))
            _health.TakeDamage(_cheatHealthAmount);

        if (Input.GetKeyDown(_increaseHealthKey) || Input.GetKeyDown(KeyCode.KeypadPlus))
            _health.Heal(_cheatHealthAmount);
    }

    private void OnGUI()
    {
        if (!_showDebug)
            return;

        float height = 45f + _lineHeight * 10f;
        Vector2 position = GetPanelPosition(height);
        float x = position.x;
        float y = position.y;
        float lineY = y + 25f;

        GUI.Box(new Rect(x, y, _width, height), "Player Debug");

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), $"Name: {gameObject.name}");
        lineY += _lineHeight + 4f;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "Health");
        lineY += _lineHeight;

        if (_health != null)
        {
            GUI.Label(
                new Rect(x + 10f, lineY, _width - 20f, _lineHeight),
                $"  HP: {_health.CurrentHP:F0} / {_health.MaxHealth:F0}");
            lineY += _lineHeight;

            GUI.Label(
                new Rect(x + 10f, lineY, _width - 20f, _lineHeight),
                $"  [{_decreaseHealthKey}] -{_cheatHealthAmount:F0} HP   [{_increaseHealthKey}] +{_cheatHealthAmount:F0} HP");
            lineY += _lineHeight + 4f;
        }
        else
        {
            GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "  (No Health found)");
            lineY += _lineHeight;
        }

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "Locomotion");
        lineY += _lineHeight;

        if (_locomotion != null)
        {
            GUI.Label(
                new Rect(x + 10f, lineY, _width - 20f, _lineHeight),
                $"  Move Multiply: {_locomotion.GetMoveMultiply():F2}");
            lineY += _lineHeight;

            GUI.Label(
                new Rect(x + 10f, lineY, _width - 20f, _lineHeight),
                $"  Locked: {_locomotion.IsMovementLocked}");
        }
        else
        {
            GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "  (No Locomotion found)");
        }
        lineY += _lineHeight + 4f;

        GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "Guard / Skill");
        lineY += _lineHeight;

        if (_blockSystem != null)
        {
            GUI.Label(
                new Rect(x + 10f, lineY, _width - 20f, _lineHeight),
                $"  Guard: {_blockSystem.CurrentGuardPoints:F0} / {_blockSystem.MaxGuardPoints:F0}");
            lineY += _lineHeight;
        }
        else
        {
            GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "  (No BlockSystem found)");
            lineY += _lineHeight;
        }

        if (_skillPoints != null)
        {
            GUI.Label(
                new Rect(x + 10f, lineY, _width - 20f, _lineHeight),
                $"  Skill: {_skillPoints.CurrentSkillPoints:F0}");
        }
        else
        {
            GUI.Label(new Rect(x + 10f, lineY, _width - 20f, _lineHeight), "  (No SkillPoints found)");
        }
    }

    private Vector2 GetPanelPosition(float height)
    {
        bool alignRight =
            _screenCorner == ScreenCorner.TopRight ||
            _screenCorner == ScreenCorner.BottomRight;

        bool alignBottom =
            _screenCorner == ScreenCorner.BottomLeft ||
            _screenCorner == ScreenCorner.BottomRight;

        float x = alignRight
            ? Screen.width - _width - _margin.x
            : _margin.x;

        float y = alignBottom
            ? Screen.height - height - _margin.y
            : _margin.y;

        return new Vector2(x, y);
    }
}
