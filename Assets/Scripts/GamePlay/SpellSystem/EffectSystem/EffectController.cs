using UnityEngine;
using UnityEngine.InputSystem;

public class EffectController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference effectAction;

    [Header("Effect")]
    [SerializeField] private EffectDefinition definition;
    [SerializeField] private Transform effectOrigin;
    [SerializeField] private Transform effectDirection;
    [SerializeField] private bool destroyAfterFinished = true;
    [SerializeField] private float destroyDelayAfterUse = 0.5f;

    [Header("Event Channels")]
    [SerializeField] private VoidEventChannelSO finishedEventChannel;
    [SerializeField] private BoolEventChannelSO forceEnabledEventChannel;

    private InputSystem_Actions generatedActions;
    private GameObject effectOwner;
    private IEffectExecutor executor;
    [SerializeField] private float remainingDuration;
    private float cooldownRemaining;
    private bool isEnabled;
    private bool isHeld;
    private bool hasExecuted;
    private bool hasFinished;
    private bool hasStarted;

    public bool IsEnabled => isEnabled;
    public bool IsActive => remainingDuration > 0f;
    public float RemainingDuration => remainingDuration;
    public float CooldownRemaining => cooldownRemaining;

    public void SetOwner(GameObject owner)
    {
        if (owner == null)
        {
            Debug.LogError("EffectController owner cannot be null.", this);
            return;
        }

        effectOwner = owner;
    }

    private void Awake()
    {
        if (definition != null)
        {
            executor = CreateExecutor(definition.Type);
        }

        if (effectAction == null)
        {
            generatedActions = new InputSystem_Actions();
        }

    }

    private void OnEnable()
    {
        if (forceEnabledEventChannel != null)
        {
            forceEnabledEventChannel.Raised += HandleForceEnabled;
        }

        SetEnabled(true);
    }

    private void OnDisable()
    {
        if (forceEnabledEventChannel != null)
        {
            forceEnabledEventChannel.Raised -= HandleForceEnabled;
        }

        SetEnabled(false);
        CancelCurrentUse();
    }

    private void OnDestroy()
    {
        generatedActions?.Dispose();
    }

    private void Update()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
        }

        if (hasStarted && remainingDuration <= 0f && !hasFinished)
        {
            isHeld = false;
            hasExecuted = false;
            FinishAndDestroy();
        }

        remainingDuration = Mathf.Max(0f, remainingDuration - Time.deltaTime);
        if (definition != null &&
            definition.ActivationMode == EffectActivationMode.HoldRepeat &&
            isHeld)
        {
            ExecuteIfReady();
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (isEnabled == enabled)
        {
            return;
        }

        isEnabled = enabled;
        InputAction action = GetAction();
        if (action == null)
        {
            return;
        }

        if (enabled)
        {
            action.started += OnInputStarted;
            action.performed += OnInputPerformed;
            action.canceled += OnInputCanceled;
            action.Enable();
        }
        else
        {
            action.started -= OnInputStarted;
            action.performed -= OnInputPerformed;
            action.canceled -= OnInputCanceled;
            action.Disable();
        }
    }

    public void OnInputStarted(InputAction.CallbackContext context)
    {
        if (!isEnabled || definition == null)
        {
            return;
        }

        hasStarted = true;
        isHeld = true;
        hasExecuted = false;

        switch (definition.ActivationMode)
        {
            case EffectActivationMode.SingleUse:
                ExecuteIfReady();
                break;
            case EffectActivationMode.RepeatDuringDuration:
                BeginDuration();
                break;
            case EffectActivationMode.HoldRepeat:
            case EffectActivationMode.HoldCommit:
                BeginDuration();
                break;
        }
    }

    public void OnInputPerformed(InputAction.CallbackContext context)
    {
        if (!isEnabled ||
            definition == null ||
            definition.ActivationMode != EffectActivationMode.RepeatDuringDuration)
        {
            return;
        }

        ExecuteIfReady();
    }

    public void OnInputCanceled(InputAction.CallbackContext context)
    {
        if (!isEnabled || definition == null)
        {
            return;
        }

        isHeld = false;
        if (definition.ActivationMode == EffectActivationMode.HoldCommit)
        {
            remainingDuration = 0f;
            hasExecuted = false;
            FinishAndDestroy();
        }
    }

    public void ExecuteEffect()
    {
        hasStarted = true;
        ExecuteIfReady();
    }

    public void ActivateFromSpawn()
    {
        if (!isEnabled || definition == null)
        {
            return;
        }

        hasStarted = true;
        isHeld = true;
        hasExecuted = false;

        switch (definition.ActivationMode)
        {
            case EffectActivationMode.SingleUse:
                ExecuteIfReady();
                break;
            case EffectActivationMode.RepeatDuringDuration:
            case EffectActivationMode.HoldRepeat:
            case EffectActivationMode.HoldCommit:
                BeginDuration();
                break;
        }
    }

    private void BeginDuration()
    {
        remainingDuration = definition.Duration;
        hasExecuted = false;
        hasFinished = false;
        ExecuteIfReady();
    }

    private void ExecuteIfReady()
    {
        if (definition == null ||
            executor == null ||
            cooldownRemaining > 0f)
        {
            return;
        }

        if (definition.ActivationMode == EffectActivationMode.SingleUse &&
            hasExecuted)
        {
            return;
        }

        if (definition.ActivationMode == EffectActivationMode.RepeatDuringDuration &&
            remainingDuration <= 0f)
        {
            return;
        }

        if ((definition.ActivationMode == EffectActivationMode.HoldRepeat ||
             definition.ActivationMode == EffectActivationMode.HoldCommit) &&
            !isHeld)
        {
            return;
        }

        executor.Execute(new EffectContext(
            effectOwner != null ? effectOwner : gameObject,
            this.gameObject,
            GetOrigin(),
            GetDirection(),
            definition));

        hasExecuted = true;
        cooldownRemaining = definition.Cooldown;

        if (definition.ActivationMode == EffectActivationMode.SingleUse)
        {
            remainingDuration = 0f;
            FinishAndDestroy(destroyDelayAfterUse);
        }
    }

    private void FinishAndDestroy(float Delay = 0)
    {
        if (hasFinished)
        {
            return;
        }

        hasFinished = true;
        finishedEventChannel?.Raise();

        if (!destroyAfterFinished)
        {
            return;
        }
        Destroy(gameObject, Delay);
    }

    private void HandleForceEnabled(bool enabled)
    {
        SetEnabled(enabled);

        if (!enabled)
        {
            Destroy(gameObject);
        }
    }

    private void CancelCurrentUse()
    {
        isHeld = false;
        remainingDuration = 0f;
        cooldownRemaining = 0f;
        hasExecuted = false;
        hasStarted = false;
    }

    private InputAction GetAction()
    {
        if (effectAction != null)
        {
            return effectAction.action;
        }

        return generatedActions?.Player.Effect;
    }

    private Vector3 GetOrigin()
    {
        return effectOrigin != null ? effectOrigin.position : transform.position;
    }

    private Vector3 GetDirection()
    {
        return effectDirection != null ? effectDirection.forward : transform.forward;
    }

    private static IEffectExecutor CreateExecutor(EffectType type)
    {
        switch (type)
        {
            case EffectType.HomingMissile:
                return new HomingMissileEffect();
            case EffectType.RadialPush:
                return new RadialPushEffect();
            case EffectType.SelfHeal:
                return new SelfHealEffect();
            default:
                Debug.LogError($"Unsupported effect type: {type}.");
                return null;
        }
    }

}
