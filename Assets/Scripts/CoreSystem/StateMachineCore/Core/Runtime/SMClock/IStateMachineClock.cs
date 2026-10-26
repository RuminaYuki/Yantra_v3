namespace Yuki.Learning.StateMachine
{
    public interface IStateMachineClock
    {
        float Now { get; }
        float DeltaTime { get; }
    }
}