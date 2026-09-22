using UnityEngine;

public class TestSubEffectBoolEventChanel : MonoBehaviour
{
    public BoolEventChannelSO raiseBoolEventChannelActionSO;
    void OnEnable()
    {
        raiseBoolEventChannelActionSO.Raised += Handle;
    }
    void OnDisable()
    {
        raiseBoolEventChannelActionSO.Raised -= Handle;
    }

    void Handle(bool _bool)
    {
        Debug.Log("BoolEventChanel "+_bool);
    }
}
