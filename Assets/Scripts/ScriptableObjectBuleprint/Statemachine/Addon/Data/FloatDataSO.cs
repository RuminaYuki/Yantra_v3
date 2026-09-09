using UnityEngine;
[CreateAssetMenu(
    fileName = "NewFloat",
    menuName = "DataSO/Variables/Float")]
public class FloatDataSO : ScriptableObject
{
    [SerializeField] private float _value;

    public float Value => _value;
}
