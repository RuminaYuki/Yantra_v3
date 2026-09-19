using UnityEngine;

public class GunControllerDebug : MonoBehaviour
{
    [SerializeField] GunController GunController;

    private void Awake()
    {
        GunController = GetComponent<GunController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            GunController.TryShooting();
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            GunController.Reload();
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            GunController.SwitchMode();
        }
    }
}
