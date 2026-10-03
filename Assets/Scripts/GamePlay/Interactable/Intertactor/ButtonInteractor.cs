using UnityEngine;

namespace SDFcl.GamePlay.Interactable
{
    public class ButtonInteractor : BaseInteractor
    {
        public override bool Interact(GameObject rootplayer, bool force = false)
        {
            if (!base.Interact(rootplayer)) return false;

            CancelInteraction(rootplayer);

            return true;
        }

        public override bool CancelInteraction(GameObject rootplayer, bool force = false)
        {
            return base.CancelInteraction(rootplayer);
        }
    }
}
