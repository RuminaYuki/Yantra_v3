using UnityEngine;
using Yuki.Learning.StateMachine;
[CreateAssetMenu(
    fileName = "MeshRendererAnchor",
    menuName = "YUKI Learning State Machine/Anchor/MeshRendererAnchor")]

public class MeshRendererAnchor : RuntimeAnchorBase<MeshRenderer> , IRuntimeAnchorBase
{
    public void IProvide(GameObject player)
    {
        MeshRenderer value = player.GetComponent<MeshRenderer>();

        base.Provide(value);
    }

    public void IUnset()
    {
        base.Unset();
    }
}


