using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public interface IEffectExecutor
{
    void Execute(GameObject owner);
}

public enum EffectType
{
    HomingMissile,
    RadialPush,
    SelfHeal
}

public enum EffectActivationMode
{
    SingleUse,
    RepeatDuringDuration,
    HoldRepeat,
    HoldCommit
}