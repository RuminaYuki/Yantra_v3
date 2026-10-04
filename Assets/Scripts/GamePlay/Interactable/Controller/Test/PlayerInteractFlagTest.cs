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

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 220, 50),
            "Press [E] to toggle interact\n" +
            "IsInteracting: " + (IsInteracting ? "ON" : "OFF"));
    }
}
