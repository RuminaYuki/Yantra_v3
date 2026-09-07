using UnityEngine;

public class GravityCharacterCon
{
    private readonly CharacterController _characterController;
    public float _velocityY;
    private float _gravityMultiplier;

    public GravityCharacterCon(CharacterController characterController, float gravityMultiplier = 1)
    {
        _characterController = characterController;
        _gravityMultiplier = gravityMultiplier;
    }
    public Vector3 Gravity()
    {
        if (_characterController.isGrounded && _velocityY < 0.0f)
        {
            _velocityY = -1.0f;
        }
        else
        {
            _velocityY += -9.81f * _gravityMultiplier * Time.deltaTime;
        }
        return Vector3.up * (_velocityY * Time.deltaTime);
    }
    #region Secondary API
    public float _GravityMultiplier{get => _gravityMultiplier; set => _gravityMultiplier = value;}
    #endregion
}
