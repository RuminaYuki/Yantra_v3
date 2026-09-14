using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", 
menuName = "StatsParameter/Character/Player/PlayerStats")]
public class PlayerStatsSO : ScriptableObject
{
    //====================Attributes======================
    [Header("====Attributes====")]
    [Header("**Max Stats**")]
    [SerializeField] private float _health_max = 10f;
    [SerializeField] private float _GuardPoints_max = 100f;
    [SerializeField] private float _skillPoints_max = 10f;
    [Header("****State****")]
    [Header("Stagger")]
    [SerializeField] private float _staggerTime = 0.3f;
    [Header("Block")]
    [SerializeField] private float _parryTime = 0.5f;
    [SerializeField] private float _ExhaustTime = 2;
    [Header("Attack")]
    [SerializeField] private AttackParameters _Attack1To3Parameters;
    [SerializeField] private float _a1To3GainSPPerHit = 0.4f;
    [SerializeField] private AttackParameters _Attack4Parameters;
    [SerializeField] private float _a4GainSPPerHit = 1;
    [SerializeField] private float _delayBeforeExitToIdle = 1;

    //====================Reference======================
    [Header("====Reference====")]
    [Header("GameObjectAnchor")]
    [SerializeField] private GameObjectAnchor _gameObjectAnchor;
    [Header("Timer Conditions")]
    [SerializeField] private CountDownTimerConditionSO _staggerCondition;
    [SerializeField] private CountDownTimerConditionSO _parryCondition;
    [SerializeField] private CountDownTimerConditionSO _exhaustCondition;
    [Header("Execute Attack Action")]
    [SerializeField] private ExecuteAttackActionSO _A1To3executeAttackActionSO;
    [SerializeField] private ExecuteAttackActionSO _A4executeAttackActionSO;
    [SerializeField] private GianSkillOnHitPointActionSO _A1To3GianSPOnHitActionSo;
    [SerializeField] private GianSkillOnHitPointActionSO _A4GianSPOnHitActionSo;
    [SerializeField] private AnimationFinishedConditionSO _animationFinishedConditionSO;


    //====================Functions======================
    public void ApplyStats()
    {
        SetMaxHealth();
        SetMaxGuardPoints();
        SetMaxSkillPoints();

        SetStaggerTime();
        SetParryTime();
        SetExhaustTime();

        SetAttack1To3Parameters();
        SetAttack1To3GainSkillPoint();
        SetAttack4Parameters();
        SetAttack4GainSkillPoint();

        SetDelayBeforeExitToIdle();
    }

    #region Helper Functions
    private void SetMaxHealth()
    {
        _gameObjectAnchor.Value.GetComponent<Health>().MaxHealth = _health_max;
    }

    private void SetMaxGuardPoints()
    {
        _gameObjectAnchor.Value.GetComponent<BlockSystem>().MaxGuardPoints = _GuardPoints_max;
    }
    private void SetMaxSkillPoints()
    {
        _gameObjectAnchor.Value.GetComponent<SkillPoints>().MaxSkillPoints = _skillPoints_max;
    }
    private void SetStaggerTime()
    {
        _staggerCondition.MinDuration = _staggerTime;
        _staggerCondition.MaxDuration = _staggerTime;
    }
    private void SetParryTime()
    {
        _parryCondition.MinDuration = _parryTime;
        _parryCondition.MaxDuration = _parryTime;
    }
    private void SetExhaustTime()
    {
        _exhaustCondition.MinDuration = _ExhaustTime;
        _exhaustCondition.MaxDuration = _ExhaustTime;
    }
    private void SetAttack1To3Parameters()
    {
        _A1To3executeAttackActionSO.AttackParameters = _Attack1To3Parameters;
    }
    private void SetAttack1To3GainSkillPoint()
    {
        _A1To3GianSPOnHitActionSo.GianSkillPointAmount = _a1To3GainSPPerHit;
    }
    private void SetAttack4Parameters()
    {
        _A4executeAttackActionSO.AttackParameters = _Attack4Parameters;
    }
    private void SetAttack4GainSkillPoint()
    {
        _A4GianSPOnHitActionSo.GianSkillPointAmount = _a4GainSPPerHit;
    }
    private void SetDelayBeforeExitToIdle()
    {
        _animationFinishedConditionSO.ExtraSeconds = _delayBeforeExitToIdle;
    }
    #endregion
}
