using UnityEngine;

public class CharacterTeleporter : MonoBehaviour
{
    private CharacterController _characterController;
    private PathNavigator _pathNavigator;
    private BaseLocomotion _locomotion;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _pathNavigator = GetComponent<PathNavigator>();
        _locomotion = GetComponent<BaseLocomotion>();
    }

    public void Teleport(Vector3 position)
    {
        PrepareTeleport();
        SetPosition(position);
    }

    public void TeleportAndFace(Vector3 position, Vector3 facePosition)
    {
        PrepareTeleport();
        SetPosition(position);
        FacePosition(facePosition);
    }

    private void PrepareTeleport()
    {
        if (_pathNavigator != null)
            _pathNavigator.ClearTarget();

        if (_locomotion != null)
            _locomotion.ClearMovementDirection();
    }

    private void SetPosition(Vector3 position)
    {
        bool controllerWasEnabled =
            _characterController != null &&
            _characterController.enabled;

        if (controllerWasEnabled)
            _characterController.enabled = false;

        transform.position = position;

        if (controllerWasEnabled)
            _characterController.enabled = true;
    }

    private void FacePosition(Vector3 position)
    {
        Vector3 direction =
            position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}