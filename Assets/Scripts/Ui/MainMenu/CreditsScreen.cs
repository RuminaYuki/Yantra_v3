using UnityEngine;
using UnityEngine.Device;
using Yantra.UI;

public class CreditsScreen : UIScreen
{
    public void OnBackClicked()
    {
        UIManager.Instance.Back();
    }
}