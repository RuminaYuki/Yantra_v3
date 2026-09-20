using UnityEngine;
using Yuki.Learning.StateMachine;
[CreateAssetMenu(
    fileName = "NewGameObject_Anchor",
    menuName = "YUKI Learning State Machine/Anchor/GameObjectAnchor")]
public class GameObjectAnchor : RuntimeAnchorBase<GameObject>, IRuntimeAnchorBase
{
    public void IProvide(GameObject gameObject)
    {
        base.Provide(gameObject);
    }

    public void IUnset()
    {
        base.Unset();
    }
}
