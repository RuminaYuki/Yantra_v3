using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", 
menuName = "StatsParameter/Character/Enemy/PKaStats")]
public class PKaStatsSO : ScriptableObject
{
    //====================Attributes======================
    [Header("====Attributes====")]
    [Header("**Max Stats**")]
    [SerializeField] private float _health_max = 10f;
    //====================Reference======================
    [Header("====Reference====")]
    [Header("GameObjectAnchor")]
    [SerializeField] private GameObjectAnchor _gameObjectAnchor;
    
    #region Helper Functions
    private void SetMaxHealth()
    {
        _gameObjectAnchor.Value.GetComponent<Health>().MaxHealth = _health_max;
    }
    #endregion
}
