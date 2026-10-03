using UnityEngine;

public class PlayerInteractFlagTest : MonoBehaviour
{
    public bool IsInteracting { get; private set; }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            IsInteracting = !IsInteracting;
        }
    }
}
