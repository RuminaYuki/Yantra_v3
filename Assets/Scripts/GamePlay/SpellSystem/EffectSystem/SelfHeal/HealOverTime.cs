using System.Collections;
using UnityEngine;

public class HealOverTime : MonoBehaviour
{
    [SerializeField] float healAmount;
    [SerializeField] float OverTime = 0f;
    Coroutine healCoroutine;
    IHeal heal;

    public void StartHealing(float heal, float time, IHeal Iheal)
    {
        healAmount = heal;
        OverTime = time;
        this.heal = Iheal;

        if (healCoroutine != null)
        {
            StopCoroutine(healCoroutine);
        }
        healCoroutine = StartCoroutine(HealOT());
    }

    IEnumerator HealOT()
    {
        if (heal == null) yield break;
        float elapsedTime = 0f;
        while (elapsedTime < OverTime)
        {
            heal.Heal(healAmount * Time.deltaTime / OverTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
}
