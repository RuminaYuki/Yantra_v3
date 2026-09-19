using UnityEngine;

[CreateAssetMenu(fileName = "NewMaxGuardPoint_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/Component/Set Max Guard Points")]
public class SetMaxGuardPointsModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private GameObjectAnchor _anchor;
    [SerializeField] private float _maxGuardPoints;

    public void Apply()
    {
        _anchor.Value.GetComponent<BlockSystem>().MaxGuardPoints = _maxGuardPoints;
    }
}
