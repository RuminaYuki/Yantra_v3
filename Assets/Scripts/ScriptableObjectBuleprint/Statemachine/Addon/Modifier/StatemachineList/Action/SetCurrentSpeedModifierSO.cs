using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCurretSpeed_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/SetCurrentSpeed")]
public class SetCurrentSpeedModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private List<SetCurrentSpeedActionSO> _target;
    [SerializeField] private float speed;
    public void Apply()
    {
        foreach (SetCurrentSpeedActionSO currentSpeedAction in _target)
        {
            currentSpeedAction.Speed = speed;
        }
    }
}
