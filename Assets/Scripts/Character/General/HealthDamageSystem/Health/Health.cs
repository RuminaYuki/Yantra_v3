using System;
using UnityEngine;
// RuminaYuki Class
public class Health : MonoBehaviour, IDamageable,IHeal
{
    [SerializeField] protected float maxHealth = 10f;
    [SerializeField] protected bool assignValue = false;
    public float MaxHealth
    {
        get => maxHealth;
        set
        {
            if (value <= 0)
            {
                Debug.LogWarning("Max health must be greater than zero.");
                return;
            }
            maxHealth = value;
            if (CurrentHP > maxHealth)
            {
                CurrentHP = maxHealth;
                OnHealthChangedEventChannel?.Raise(CurrentHP);
                OnHealthChanged?.Invoke(CurrentHP);
            }
        }
    }
    
    [field: SerializeField]
    public float CurrentHP { get; protected set; }
    public bool IsDead => CurrentHP <= 0;

    public VoidEventChannelSO OnDeadEventChannel;
    public FloatEventChannelSO OnHealthChangedEventChannel;
    public VoidEventChannelSO OnHurtEventChannel;
    public VoidEventChannelSO OnHitEventChannel;

    public event Action OnDead;
    public event Action<float> OnHealthChanged;
    public event Action OnHurt;
    public event Action OnHit;
    public event Action<DamageTypeID> OnHitDamageType;
    public event Action<DamageTypeID> OnHurtDamageType;

    private bool _lastDamageApplied;

    protected virtual void Awake()
    {
        CurrentHP = maxHealth;
    }

    public virtual void Heal(float amount)
    {
        if (IsDead || IgnoreDamage) return;

        CurrentHP += amount;
        if (CurrentHP > maxHealth)
            CurrentHP = maxHealth;
        OnHealthChangedEventChannel?.Raise(CurrentHP);
        OnHealthChanged?.Invoke(CurrentHP);
        Debug.Log($"{gameObject.name} <color=#32CD32> healed {amount}.</color> Current HP: {CurrentHP}/{maxHealth}");
    }

    public virtual void TakeDamage(float damage)
    {
        OnHitEventChannel?.Raise();
        OnHit?.Invoke();

        _lastDamageApplied = !(IsDead || IgnoreDamage);
        if (!_lastDamageApplied) return;

        CurrentHP -= damage;
        OnHealthChangedEventChannel?.Raise(CurrentHP);
        OnHealthChanged?.Invoke(CurrentHP);
        OnHurtEventChannel?.Raise();
        OnHurt?.Invoke();

        Debug.Log($"{gameObject.name} took {damage} damage. Current HP: {CurrentHP}/{maxHealth}");
        if (CurrentHP <= 0)
        {
            Debug.Log($"{gameObject.name} is dead.");
            CurrentHP = 0;
            Dead();
        }
    }
    public void DamageType(DamageTypeID type)
    {
        OnHitDamageType?.Invoke(type);

        if (_lastDamageApplied)
            OnHurtDamageType?.Invoke(type);
    }

    public void Kill()
    {
        if (IsDead) return;

        CurrentHP = 0;
        OnHealthChangedEventChannel?.Raise(CurrentHP);
        OnHealthChanged?.Invoke(CurrentHP);
        Dead();
    }

    private void Dead()
    {
        //if (SaveManager.Instance != null && this.gameObject.CompareTag("Player")) SaveManager.Instance.LoadAll();
        OnDeadEventChannel?.Raise();
        OnDead?.Invoke();
    }

    public bool IgnoreDamage { get; private set; } = false;
    public void SetEnableIgnoreDamage(bool enable)
    {
        IgnoreDamage = enable;
    }
    
    public void SetCurrentHealth(float amount)
    {
        CurrentHP = Mathf.Clamp(amount, 0f, maxHealth);
        OnHealthChangedEventChannel?.Raise(CurrentHP);
        OnHealthChanged?.Invoke(CurrentHP);
    }

    public void RestoreFullHealth()
    {
        CurrentHP = maxHealth;
        OnHealthChangedEventChannel?.Raise(CurrentHP);
        OnHealthChanged?.Invoke(CurrentHP);
    }
}
