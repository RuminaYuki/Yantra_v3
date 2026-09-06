using UnityEngine;
namespace Yuki.Learning.StateMachine
{
    public interface IRuntimeAnchorBase
    {
        //Kuy
        void IProvide(GameObject player);
        void IUnset();
    }
}