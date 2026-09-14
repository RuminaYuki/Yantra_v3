using UnityEngine;

public class TestTriggerBattleState_GameState : MonoBehaviour
{
    public TriggerConditionSO triggerConditionSO;

    // Update is called once per frame
    void OnGUI()
    {
        if (GUI.Button(new Rect(10,10, 200, 30), "Trigger Battle State"))
        {
            triggerConditionSO.Trigger();
        }
    }
}
