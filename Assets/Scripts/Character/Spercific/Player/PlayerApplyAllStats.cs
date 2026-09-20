using UnityEngine;

public class PlayerApplyAllStats : MonoBehaviour
{
    [SerializeField] CharacterStatsSO playerStatsSO;
    void Awake()
    {
        playerStatsSO.ApplyStats();
    }
}
