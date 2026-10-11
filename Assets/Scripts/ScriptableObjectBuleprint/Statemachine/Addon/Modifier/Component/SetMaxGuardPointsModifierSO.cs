using UnityEngine;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(fileName = "NewMaxGuardPoint_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/Component/Set Max Guard Points")]
public class SetMaxGuardPointsModifierSO : ScriptableObject, IOwnerStatModifier
{
    [Tooltip("Leave empty to use the character itself")]
    [SerializeField] private AnchorIDSO _anchor;
    [SerializeField] private float _maxGuardPoints;

    public void Apply(GameObject owner)
    {
        if (ComponentModifierTarget.TryGet(owner, _anchor, this, out BlockSystem blockSystem))
            blockSystem.MaxGuardPoints = _maxGuardPoints;
    }
}
