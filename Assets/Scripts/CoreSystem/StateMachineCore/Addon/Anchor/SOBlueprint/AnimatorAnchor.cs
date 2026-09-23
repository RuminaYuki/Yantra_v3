using UnityEngine;
using Yuki.Learning.StateMachine;
[CreateAssetMenu(
    fileName = "AnimatorAnchor",
    menuName = "YUKI Learning State Machine/Anchor/AnimatorAnchor")]

public class AnimatorAnchor : RuntimeAnchorBase<Animator> , IRuntimeAnchorBase
{
    public void IProvide(GameObject player)
    {
        Animator value = player.GetComponent<Animator>();

        base.Provide(value);
    }

    public void IUnset()
    {
        base.Unset();
    }
}


