using System;
using System.Collections.Generic;
using UnityEngine;

public class TargetTranfomeModifier : MonoBehaviour
{
    [SerializeField] private TargetDetector detector;
    [SerializeField] private List<DataModify> dataModifies = new();
    private readonly Dictionary<Transform, Transform> offsetTargets = new();

    private void Awake()
    {
        if (detector == null &&
            !TryGetComponent(out detector))
        {
            Debug.LogError($"{name} requires a TargetDetector.", this);
            return;
        }

        detector.AddModifier(Modify);
    }

    private Transform Modify(Transform target)
    {
        if (target == null)
            return null;

        PrefabIdentity targetIdentity =
            target.GetComponentInParent<PrefabIdentity>();

        if (targetIdentity == null || string.IsNullOrEmpty(targetIdentity.PrefabId))
            return target;

        foreach (DataModify data in dataModifies)
        {
            if (data.Prefab == null)
                continue;

            PrefabIdentity prefabIdentity =
                data.Prefab.GetComponentInChildren<PrefabIdentity>();

            if (prefabIdentity == null ||
                !string.Equals(targetIdentity.PrefabId, prefabIdentity.PrefabId,
                    StringComparison.Ordinal))
                continue;

            if (offsetTargets.TryGetValue(target, out Transform existingOffset) &&
                existingOffset != null)
            {
                return existingOffset;
            }

            GameObject offsetTarget = new($"{target.name}_TargetOffset");
            Transform offsetTransform = offsetTarget.transform;
            offsetTransform.SetParent(target, false);
            offsetTransform.localPosition = data.Offset;
            offsetTransform.localRotation = Quaternion.identity;
            offsetTargets[target] = offsetTransform;

            return offsetTransform;
        }

        return target;
    }
}

[Serializable]
public struct DataModify
{
    public GameObject Prefab;
    public Vector3 Offset;
}