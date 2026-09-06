using UnityEngine;
namespace Yuki.Learning.StateMachine
{
    public interface IRuntimeAnchorBase
    {
        //Kuy67
        void IProvide(GameObject player);
        void IUnset();
    }
}