using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Sets what the PatternSpawner spawns when the state is entered; an Animation Event calling its
// Execute does the spawning, on the frame the animation needs it.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PatternSpawner))]
[CreateAssetMenu(
    fileName = "NewSetSpawnPattern_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Spawner/Set Spawn Pattern")]
public class SetSpawnPatternActionSO : StateActionSO
{
    [Tooltip("What gets spawned at every point.")]
    [SerializeField] private GameObject _prefab;
    [Tooltip("The pattern prefab with SpawnerData (e.g. SpawnMinnion).")]
    [SerializeField] private SpawnerData _pattern;
    [Tooltip("Where the pattern is placed. Empty = the character running this state machine.")]
    [SerializeField] private AnchorIDSO _anchor;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetSpawnPatternAction(_prefab, _pattern, _anchor);
    }
}

public class SetSpawnPatternAction : StateAction
{
    private readonly GameObject _prefab;
    private readonly SpawnerData _pattern;
    private readonly AnchorIDSO _anchorID;
    private AnchorReference<PatternSpawner> _spawner;
    private AnchorReference<Transform> _anchor;

    public SetSpawnPatternAction(GameObject prefab, SpawnerData pattern, AnchorIDSO anchorID)
    {
        _prefab = prefab;
        _pattern = pattern;
        _anchorID = anchorID;
    }

    public override void Awake(StateMachine stateMachine)
    {
        // Self Anchor empty = the spawner on this character; set = one on another object.
        _spawner = new AnchorReference<PatternSpawner>(stateMachine, this);
        _anchor = new AnchorReference<Transform>(stateMachine, _anchorID, this);
    }

    public override void OnStateEnter()
    {
        if (_spawner.TryGet(out PatternSpawner spawner) && _anchor.TryGet(out Transform anchor))
        {
            spawner.SetParameter(_prefab, _pattern, anchor);
        }
    }

    public override void OnUpdate() { }

    public override void OnStateExit() { }
}
