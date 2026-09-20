using UnityEngine;

public class ApplyAllStats : MonoBehaviour
{
    [SerializeField] CharacterStatsSO CharacterStatsSO;
    void Awake()
    {
        CharacterStatsSO.ApplyStats();
    }
}
