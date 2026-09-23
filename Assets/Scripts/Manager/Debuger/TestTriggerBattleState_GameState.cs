using UnityEngine;

public class TestTriggerBattleState_GameState : MonoBehaviour
{
    public TriggerConditionSO triggerConditionSO;
    public CountDownTimerConditionSO countDownTimerConditionSO;

    // Update is called once per frame
    void OnGUI()
    {
        if (GUI.Button(new Rect(10,10, 200, 30), "Trigger Battle State"))
        {
            triggerConditionSO.Trigger();
            countDownTimerConditionSO.ResetTimer();
        }
        GUI.Label(new Rect(10, 50, 300, 30), 
        $"Remaining Time: {countDownTimerConditionSO.Remaining:F2}/{countDownTimerConditionSO.Duration:F2} seconds");
    }
}
