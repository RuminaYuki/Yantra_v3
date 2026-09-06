using UnityEngine;
namespace Yuki.Learning.StateMachine
{
    public interface IRuntimeAnchorBase
    {
        //ควย
        void IProvide(GameObject player);
        void IUnset();
    }
}