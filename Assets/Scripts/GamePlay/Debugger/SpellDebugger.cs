using UnityEngine;

public class SpellDebugger : MonoBehaviour
{
    public SpellController spellController;
    public bool tryActive;

    // Update is called once per frame
    void Update()
    {
        spellController.SetActive(tryActive);
    }
}
