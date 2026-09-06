using UnityEngine;
namespace Yuki.Learning.StateMachine
{
    public interface IRuntimeAnchorBase
    {
        void IProvide(GameObject player);
        void IUnset();
    }
}