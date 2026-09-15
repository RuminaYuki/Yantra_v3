using UnityEngine;

public class HandleShoting : MonoBehaviour
{
    [SerializeField] VoidEventChannelSO _startShooting;

    public void shooting()
    {
        _startShooting.Raise();
    }
}
