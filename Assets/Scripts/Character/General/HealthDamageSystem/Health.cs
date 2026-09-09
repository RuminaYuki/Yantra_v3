using System;
using UnityEngine;
// RuminaYuki Class
public class Health : MonoBehaviour, IDamageable,IHeal
{
    [SerializeField] protected float maxHealth = 10f;
    public float MaxHealth => maxHealth;
    
    [field: SerializeField]
    public float CurrentHP { get; protected set; }
    public bool IsDead => CurrentHP <= 0;

    public VoidEventChannelSO OnDead;
    public FloatEventChannelSO OnHealthChanged;
    public VoidEventChannelSO OnHurt;
    public VoidEventChannelSO OnHit;

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
        OnHealthChanged?.Raise(CurrentHP);
        Debug.Log($"{gameObject.name} <color=#32CD32> healed {amount}.</color> Current HP: {CurrentHP}/{maxHealth}");
    }

    public virtual void TakeDamage(float damage)
    {
        OnHit?.Raise();
        if (IsDead || IgnoreDamage) return;

        CurrentHP -= damage;
        OnHealthChanged?.Raise(CurrentHP);
        OnHurt?.Raise();

        Debug.Log($"{gameObject.name} took {damage} damage. Current HP: {CurrentHP}/{maxHealth}");
        if (CurrentHP <= 0)
        {
            Debug.Log($"{gameObject.name} is dead.");
            CurrentHP = 0;
            Dead();
        }
    }

    public void Kill()
    {
        if (IsDead) return;

        CurrentHP = 0;
        OnHealthChanged?.Raise(CurrentHP);
        Dead();
    }

    private void Dead()
    {
        //if (SaveManager.Instance != null && this.gameObject.CompareTag("Player")) SaveManager.Instance.LoadAll();
        OnDead?.Raise();
    }

    public bool IgnoreDamage { get; private set; } = false;
    public void SetEnableIgnoreDamage(bool enable)
    {
        IgnoreDamage = enable;
    }
    
    public void SetCurrentHealth(float amount)
    {
        CurrentHP = Mathf.Clamp(amount, 0f, maxHealth);
        OnHealthChanged?.Raise(CurrentHP);
    }

    public void RestoreFullHealth()
    {
        CurrentHP = maxHealth;
        OnHealthChanged?.Raise(CurrentHP);
    }
}
