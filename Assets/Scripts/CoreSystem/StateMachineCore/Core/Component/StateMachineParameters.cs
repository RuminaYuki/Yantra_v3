using System;
using System.Collections.Generic;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// วางคู่กับ StateMachineController: ค่าที่ตัวละครนี้ override จาก parameter ของ table
// (เหมือนช่อง Properties ใน VisualEffect) เก็บเฉพาะตัวที่ติ๊ก อ้างด้วยชื่อ parameter
[DisallowMultipleComponent]
[RequireComponent(typeof(StateMachineController))]
public class StateMachineParameters : MonoBehaviour
{
    [SerializeField] private List<ParameterOverride> _overrides = new List<ParameterOverride>();
    public IReadOnlyList<ParameterOverride> Overrides => _overrides;

    // blackboard ของ table ที่ state machine ของตัวละครนี้ใช้ (Variant: ของ Base ด้วย)
    private readonly List<ExposedParametersSO> _blackboards = new List<ExposedParametersSO>();

    public static void Register(StateMachine stateMachine, ExposedParametersSO blackboard)
    {
        var parameters = stateMachine.Owner.GetComponent<StateMachineParameters>();
        if (parameters == null || blackboard == null)
        {
            return;
        }

        if (!parameters._blackboards.Contains(blackboard))
        {
            parameters._blackboards.Add(blackboard);
        }
    }

    // ใส่ค่าของตัวละครทับลงบน result (ElementResolver เรียก หลังใส่ค่าของ Variant แล้ว)
    public T ApplyOverrides<T>(T original, T result) where T : StateElementSO
    {
        foreach (ExposedParametersSO blackboard in _blackboards)
        {
            foreach (ExposedParameter parameter in blackboard.Parameters)
            {
                foreach (ExposedBinding binding in parameter.Bindings)
                {
                    // ไม่ใช่ SO ตัวนี้
                    if (binding.Target != original)
                    {
                        continue;
                    }

                    // ตัวละครนี้ไม่ได้ติ๊ก parameter นี้
                    ParameterOverride found = FindOverride(parameter.Name);
                    if (found == null)
                    {
                        continue;
                    }

                    // ยังไม่มีใคร copy (Variant ไม่ได้ override SO ตัวนี้) → copy ตอนนี้
                    // จะได้ไม่เขียนทับ asset ตัวจริง
                    if (result == original)
                    {
                        result = Instantiate(original);
                    }

                    FieldPathUtility.Apply(result, binding.FieldName, found);
                }
            }
        }

        return result;
    }

    private ParameterOverride FindOverride(string parameterName)
    {
        foreach (ParameterOverride parameterOverride in _overrides)
        {
            if (parameterOverride.Name == parameterName)
            {
                return parameterOverride;
            }
        }

        return null;
    }
}

// ช่องเก็บค่าที่ override ใช้ร่วมกัน: ตัวละคร (ParameterOverride) และ Variant (FieldOverride)
// มีหลายช่องเพราะไม่รู้ล่วงหน้าว่า field เป็นชนิดไหน FieldPathUtility เลือกช่องที่ตรงชนิดให้เอง
[Serializable]
public class OverrideValue
{
    [SerializeField] private float _value;                 // float, int, enum
    [SerializeField] private bool _bool;
    [SerializeField] private string _string;
    [SerializeField] private Vector3 _vector;              // Vector3, Vector2
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private UnityEngine.Object _object;   // SO, AnimationClip, InputActionReference ...

    public float Value => _value;
    public bool BoolValue => _bool;
    public string StringValue => _string;
    public Vector3 Vector => _vector;
    public Color ColorValue => _color;
    public UnityEngine.Object ObjectValue => _object;
}

// ค่าของตัวละคร: อ้าง parameter ด้วยชื่อ
[Serializable]
public class ParameterOverride : OverrideValue
{
    [SerializeField] private string _name;

    public string Name => _name;
}
