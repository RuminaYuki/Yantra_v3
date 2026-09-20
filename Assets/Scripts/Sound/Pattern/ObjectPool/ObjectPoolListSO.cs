using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ObjectPooler/ObjectPoolList")]
public class ObjectPoolListSO : ScriptableObject
{
    public List<Pool> pools;
}