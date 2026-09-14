using UnityEngine;

public class PlayerApplyAllStats : MonoBehaviour
{
    [SerializeField] PlayerStatsSO playerStatsSO;
    void Awake()
    {
        playerStatsSO.ApplyStats();
    }
}
