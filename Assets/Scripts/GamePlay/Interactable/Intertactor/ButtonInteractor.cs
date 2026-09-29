using UnityEngine;

public class ButtonInteractor : BaseInteractor
{
    public override bool Interact(GameObject rootplayer)
    {
        if (!base.Interact(rootplayer)) return false;

        Debug.Log("Button Down");

        CancelInteraction(rootplayer);

        return true;
    }

    public override bool CancelInteraction(GameObject rootplayer)
    {
        return base.CancelInteraction(rootplayer);
    }
}
