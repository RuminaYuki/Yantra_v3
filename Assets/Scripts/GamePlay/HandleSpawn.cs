using UnityEngine;

public class HandleSpawn : MonoBehaviour
{
    [SerializeField] VoidEventChannelSO _startSpawn;

    public void spawn()
    {
        _startSpawn.Raise();
    }

    public VoidEventChannelSO GetEventChannel() => _startSpawn;
}
