using UnityEngine;

/// <summary>
/// For modifiers that change a component on the character itself (e.g. Health.MaxHealth),
/// so they need to know which character. Plain IStatModifier tunes shared SOs and doesn't.
/// </summary>
public interface IOwnerStatModifier
{
    void Apply(GameObject owner);
}
