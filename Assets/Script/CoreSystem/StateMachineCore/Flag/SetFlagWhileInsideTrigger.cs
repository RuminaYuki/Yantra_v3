using UnityEngine;

public class SetFlagWhileInsideTrigger : MonoBehaviour
{
    [SerializeField] private FlagSO flag;

    private void OnTriggerStay(Collider other)
    {
        StateFlagsAccess stateFlags =
            other.GetComponentInParent<StateFlagsAccess>();

        if (stateFlags == null || flag == null)
            return;

        stateFlags.Set(flag, true);
    }

    private void OnTriggerExit(Collider other)
    {
        StateFlagsAccess stateFlags =
            other.GetComponentInParent<StateFlagsAccess>();

        if (stateFlags == null || flag == null)
            return;

        stateFlags.Set(flag, false);
    }
}
