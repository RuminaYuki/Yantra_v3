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

    protected override SaveData Capture() => new SaveData
    {
        isDead = health != null ? health.IsDead : false
    };

    protected override void Restore(SaveData state, int version)
    {
        if (health != null && state.isDead)
        {
            health.SetCurrentHealth(0f);

            if (disableOnDeath)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
