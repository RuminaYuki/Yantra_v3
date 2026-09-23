using UnityEngine;

[CreateAssetMenu(fileName = "NewPathNavigatorOffset_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Path Navigator Offset")]
public class SetPathNavigatorOffsetModifierSO : ScriptableObject, IStatModifier, IGizmoModule
{
    [SerializeField] private SetPathNavigatorTargetActionSO _target;
    [SerializeField] private Vector3 _offset;
    [Header("Gizmos")]
    [SerializeField] private bool _enabledGizmos = false;
    [SerializeField] private Color _gizmoColor = Color.yellow;
    public bool GizmoEnabled => _enabledGizmos;
    public void Apply()
    {
        _target.Offset = _offset;
    }
    public void DrawGizmos(Transform origin)
    {
        Gizmos.color = _gizmoColor;
        Transform targetTransform = _target != null ? _target.TargetTransform : null;

        if (targetTransform == null)
        {
            // _targetAnchor.Value เป็น null จนกว่าจะมีคน Provide() ตอน runtime
            // ใช้ tag lookup แทนเพื่อพรีวิวตอน edit mode (เหมือน LineOfSight.Start())
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                targetTransform = player.transform;
        }

        if (targetTransform == null) return;

        Vector3 previewPos = targetTransform.TransformPoint(_offset);
        Gizmos.DrawLine(targetTransform.position, previewPos);
        Gizmos.DrawWireSphere(previewPos, 0.25f);
    }
    
}
