public interface ILocomotionMoveMode
{
    LocomotionMoveMode MoveMode { get; set; }
}
public enum LocomotionMoveMode
{
    Velocity,       // move by code
    RootMotion,     // position by root motion, rotation by code
    FullRootMotion, // position + rotation by root motion
    External        // position + rotation by other code, locomotion doesn't move or rotate
}