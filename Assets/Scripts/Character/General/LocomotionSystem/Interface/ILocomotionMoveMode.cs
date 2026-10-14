public interface ILocomotionMoveMode
{
    LocomotionMoveMode MoveMode { get; set; }
}
public enum LocomotionMoveMode
{
    Velocity,       // move by code
    RootMotion,     // position by root motion, rotation by code
    FullRootMotion, // position + rotation by root motion
    External        // move by other code locomotion dont move
}