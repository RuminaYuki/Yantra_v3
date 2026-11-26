using System;
using UnityEngine;

[RequireComponent(typeof(SaveId))]
public class MonsterSaveable : SaveableBehaviour<MonsterSaveable.SaveData>
{
    [Serializable]
    public class SaveData
    {
        public bool isDead;
    }

    [SerializeField] Health health;
    [SerializeField] bool disableOnDeath = false;
    protected override string DefaultKey => "monster_" + GetComponent<SaveId>().Id;

    private void Awake()
    {
        if (health == null) health = GetComponentInChildren<Health>(true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (health != null) health.OnDead += RecordDeath;
    }

    protected override void OnDisable()
    {
        if (health != null) health.OnDead -= RecordDeath;
        base.OnDisable();
    }

    protected override SaveData Capture() => new SaveData
    {
        isDead = health != null ? health.IsDead : false
    };

    private void RecordDeath() => RecordNow();

    protected override void Restore(SaveData state, int version)
    {
        if (health != null && state.isDead)
        {
            health.Kill();

            if (disableOnDeath)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
