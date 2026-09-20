using UnityEngine;

[CreateAssetMenu(fileName = "NewMaxHealth_Modifier", 
menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/Component/Set Max Health")]
public class SetMaxHealthModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private GameObjectAnchor _anchor;
    [SerializeField] private float _maxHealth;

    public void Apply()
    {
        _anchor.Value.GetComponent<Health>().MaxHealth = _maxHealth;
    }
}
