using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ObjectPooler/ObjectPoolTableSO")]
public class ObjectPoolTableSO : ScriptableObject
{
    public List<ObjectPoolListSO> poolTableList;
}