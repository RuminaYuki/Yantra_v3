using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class QuestEnemyTracker : MonoBehaviour
{
    [SerializeField] bool canAddEnemy = false;
    [SerializeField] List<GameObject> SetupList = new List<GameObject>();
    public UnityEvent OnDead;

    List<GameObject> ListEnemys = new List<GameObject>();

    private void Awake()
    {
        SetupSupAction();
    }

    private void OnEnemyDead()
    {
        OnDead?.Invoke();
    }

    private void SetupSupAction()
    {
        foreach (var enemy in SetupList)
        {
            AddEnemyToList(enemy);
        }
    }

    private bool AddEnemyToList(GameObject enemy)
    {
        if (!ListEnemys.Contains(enemy))
        {
            var health = enemy.GetComponentInChildren<Health>();
            if (health != null)
            {
                health.OnDead += OnEnemyDead;
                ListEnemys.Add(enemy);
                return true;
            }
        }
        return false;
    }

    public bool AddEnemy(GameObject enemy)
    {
        if (canAddEnemy)
        {
            return AddEnemyToList(enemy);
        }
        return false;
    }
}
