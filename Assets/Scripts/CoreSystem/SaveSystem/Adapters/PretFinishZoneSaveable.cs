using UnityEngine;

[DefaultExecutionOrder(-40)]
[RequireComponent(typeof(SaveId))]
public class PretFinishZoneSaveable : SaveableBehaviour<PretFinishZoneSaveable.PretFinishZoneState>
{
    [System.Serializable]
    public class PretFinishZoneState
    {
        public bool isFinished;
    }

    private SaveId saveId;
    private bool isFinished;

    protected override string DefaultKey
    {
        get
        {
            if (saveId == null) saveId = GetComponent<SaveId>();
            return saveId != null && saveId.HasId ? $"pretFinishZone_{saveId.Id}" : null;
        }
    }

    public void SetFinished(bool finished)
    {
        isFinished = finished;
        //ApplyFinishedState();
        RecordNow();
    }

    protected override PretFinishZoneState Capture() => new PretFinishZoneState { isFinished = this.isFinished };

    protected override void Restore(PretFinishZoneState state, int version)
    {
        isFinished = state.isFinished;
        ApplyFinishedState();
    }

    private void ApplyFinishedState()
    {
        if (isFinished)
        {
            this.gameObject.SetActive(false);
        }
    }
}
