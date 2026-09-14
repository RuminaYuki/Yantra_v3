using UnityEngine;
using System.Collections;

public class BattleStateSubscribeEvent : MonoBehaviour
{
    public VoidEventChannelSO battleStateTriggerEventChannel;
    bool showGUI = false;
    void OnEnable()
    {
        battleStateTriggerEventChannel.Raised += HandleTriggerBattleStateEvent;
    }
    void OnDisable()
    {
        battleStateTriggerEventChannel.Raised -= HandleTriggerBattleStateEvent;
    }

    void OnGUI()
    {
        if(!showGUI) return;
        float labelWidth = 300, labelHeight = 30;
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        GUI.Label(new Rect(centerX - labelWidth / 2f, centerY + 5f, labelWidth, labelHeight), $"Trigger Battle State Event");
    }
    IEnumerator ShowGUI()
    {
        showGUI = true;
        yield return new WaitForSeconds(3f);
        showGUI = false;
    }
    void HandleTriggerBattleStateEvent()
    {
        StartCoroutine(ShowGUI());
    }
}
