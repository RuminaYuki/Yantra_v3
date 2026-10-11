using UnityEngine;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(fileName = "NewMaxHealth_Modifier",
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/Component/Set Max Health")]
public class SetMaxHealthModifierSO : ScriptableObject, IOwnerStatModifier
{
    [Tooltip("Leave empty to use the character itself")]
    [SerializeField] private AnchorIDSO _anchor;
    [SerializeField] private float _maxHealth;

    public void Apply(GameObject owner)
    {
        if (ComponentModifierTarget.TryGet(owner, _anchor, this, out Health health))
            health.MaxHealth = _maxHealth;
    }
}
