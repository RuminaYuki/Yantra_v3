using UnityEngine;

public class FadeObjectDebugger : MonoBehaviour
{
    public FadeObject fadeObject;
    void Start()
    {
        fadeObject.PlayFade(2f, 0f, 1f);
    }
}
