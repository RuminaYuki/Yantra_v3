using UnityEngine;

namespace SDFcl.Inventory.TestStateMachine
{
    // Test helper: logs every main-state change of a StateMachineController.
    // Put it next to the StateMachineController, press Play, open/close the inventory
    // and look for "PlayerInventory_State" in the Console. Safe to remove.
    public class InventoryStateMachineDebug : MonoBehaviour
    {
        [SerializeField] private StateMachineController controller;

        private void Reset() => controller = GetComponent<StateMachineController>();

        private void OnEnable()
        {
            if (controller != null) controller.MainStateChanged += Log;
        }

        private void OnDisable()
        {
            if (controller != null) controller.MainStateChanged -= Log;
        }

        private void Log(string from, string to) =>
            Debug.Log($"[StateMachine] {from} -> {to}", controller);
    }
}
