using System;
using UnityEngine;
using static TriggerZoneSaveable;

[RequireComponent(typeof(SaveId), typeof(TriggerZone))]
public class TriggerZoneSaveable : SaveableBehaviour<TriggerZoneData>
{
    [Serializable]
    public class TriggerZoneData
    {
        public bool isTriggered;
    }
    
    private TriggerZone triggerZone;
    private SaveId saveId;
    private bool isTriggered;

    protected override string DefaultKey => "triggerZone_" + GetComponent<SaveId>().Id;

    private void Awake()
    {
        triggerZone = GetComponent<TriggerZone>();
    }

    public void HandleTriggered(GameObject other)
    {
        isTriggered = true;
    }

    protected override TriggerZoneData Capture() => new TriggerZoneData
    {
        isTriggered = this.isTriggered
    };

    protected override void Restore(TriggerZoneData state, int version)
    {
        if (state.isTriggered)
        {
            isTriggered = true;
            this.gameObject.SetActive(false);
        }
    }
}
