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
    public VoidEventChannelSO Onhit;

    protected void DeadEvent() => OnDead?.Raise();
    protected void HealthChangedEvent() => OnHealthChanged?.Raise(CurrentHP);

    protected virtual void Awake()
    {
        CurrentHP = maxHealth;
    }
    private void Update()
    {
        // Debugging purpose only, remove this in production
        if (Input.GetKeyDown(KeyCode.F2))
        {
            TakeDamage(1f);
        }       
        if (Input.GetKeyDown(KeyCode.F3))
        {
            if (this.CompareTag("Player")) return;
            Kill();
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (IsDead)
            {
                CurrentHP = maxHealth; // Revive with full health
            }
            Heal(1f); // Heal by 1
        }
    }

    public virtual void Heal(float amount)
    {
        if (IsDead || IgnoreDamage) return;

        CurrentHP += amount;
        if (CurrentHP > maxHealth)
            CurrentHP = maxHealth;
        HealthChangedEvent();
        Debug.Log($"{gameObject.name} <color=#32CD32> healed {amount}.</color> Current HP: {CurrentHP}/{maxHealth}");
    }

    public virtual void TakeDamage(float damage)
    {
        if (IsDead || IgnoreDamage) return;

        CurrentHP -= damage;
        HealthChangedEvent();
        Onhit?.Raise();

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
        HealthChangedEvent();
        Dead();
    }

    private void Dead()
    {
        //if (SaveManager.Instance != null && this.gameObject.CompareTag("Player")) SaveManager.Instance.LoadAll();
        DeadEvent();
    }

    public bool IgnoreDamage { get; private set; } = false;
    public void EnableIgnoreDamage(bool enable)
    {
        IgnoreDamage = enable;
    }
    
    public void SetCurrentHealth(float amount)
    {
        CurrentHP = Mathf.Clamp(amount, 0f, maxHealth);
        HealthChangedEvent();
    }

    public void RestoreFullHealth()
    {
        CurrentHP = maxHealth;
        HealthChangedEvent();
    }
}
