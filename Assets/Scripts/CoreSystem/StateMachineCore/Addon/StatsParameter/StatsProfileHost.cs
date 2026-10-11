using UnityEngine;

public class StatsProfileHost : MonoBehaviour
{
    [SerializeField] StatsProfileSO CharacterStatsSO;
    void Awake()
    {
        if (CharacterStatsSO != null)
            CharacterStatsSO.ApplyStats(gameObject);
    }
     private void OnDrawGizmos()
    {
        if (CharacterStatsSO != null)
            CharacterStatsSO.DrawGizmos(transform);
    }
}
