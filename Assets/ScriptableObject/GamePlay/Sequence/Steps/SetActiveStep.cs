using System.Collections;
using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;

public class SetActiveStep : SequenceStep
{
    [SerializeField] GameObject target;
    [SerializeField] bool isActive = true;

    public override IEnumerator Run()
    {
        switch (isActive)
        {
            case true:
                target.SetActive(true);
                break;
            case false:
                target.SetActive(false);
                break;
        }
        yield break;
    }
}
