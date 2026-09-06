using UnityEngine;

public abstract class StateFlagsAccess : MonoBehaviour
{
    public abstract bool Get(FlagSO flag);
    public abstract void Set(FlagSO flag, bool value);
}
