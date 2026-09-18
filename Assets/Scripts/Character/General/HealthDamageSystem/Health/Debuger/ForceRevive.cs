using UnityEngine;

public class ForceRevive : MonoBehaviour
{
    Health health;
    void Awake()
    {
        health = GetComponent<Health>();
    }

    // Update is called once per frame
    void Update()
    {
        if (health.IsDead)
        {
            health.RestoreFullHealth();
            Debug.Log($"{gameObject.name} has been forcefully revived.");
        }
    }
}
