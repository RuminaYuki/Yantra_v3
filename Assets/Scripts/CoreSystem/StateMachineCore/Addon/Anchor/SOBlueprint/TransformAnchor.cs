using UnityEngine;
using Yuki.Learning.StateMachine;

[CreateAssetMenu(
    fileName = "TransformAnchor",
    menuName = "YUKI Learning State Machine/Anchor/TransformAnchor")]
public class TransformAnchor : RuntimeAnchorBase<Transform>, IRuntimeAnchorBase
{
    public void IProvide(GameObject gameObject)
    {
        Transform value = gameObject.transform.GetComponent<Transform>();

        base.Provide(value);
    }

    public void IUnset()
    {
        base.Unset();
    }
}
