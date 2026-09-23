using UnityEngine;

public class StatsProfileHost : MonoBehaviour
{
    [SerializeField] StatsProfileSO CharacterStatsSO;
    void Awake()
    {
        CharacterStatsSO.ApplyStats();
    }
     private void OnDrawGizmos()
    {
        if (CharacterStatsSO != null)
            CharacterStatsSO.DrawGizmos(transform);
    }
}
