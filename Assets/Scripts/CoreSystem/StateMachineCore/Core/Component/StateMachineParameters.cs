using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
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

    private readonly List<ExposedParametersSO> _blackboards = new List<ExposedParametersSO>();
    private readonly Dictionary<StateElementSO, StateElementSO> _resolved 
        = new Dictionary<StateElementSO, StateElementSO>();

    public static void Register(StateMachine stateMachine, ExposedParametersSO blackboard)
    {
        var parameters = stateMachine.Owner.GetComponent<StateMachineParameters>();
        if (parameters == null || blackboard == null)
            return; 
        if (!parameters._blackboards.Contains(blackboard))
            parameters._blackboards.Add(blackboard);
    }
    public static T Resolve<T>(StateMachine stateMachine, T original) where T : StateElementSO
    {
        var parameters = stateMachine.Owner.GetComponent<StateMachineParameters>();
        if (parameters == null || original == null)
            return original;
        return parameters.ResolveOwn(original);
    }

    private T ResolveOwn<T>(T original) where T : StateElementSO
    {
        if (_resolved.TryGetValue(original, out StateElementSO done))
            return (T)done;

        T result = original;

        foreach (ExposedParametersSO blackboard in _blackboards)
        foreach (ExposedParameter parameter in blackboard.Parameters)
        foreach (ExposedBinding binding in parameter.Bindings) 
        {
            if (binding.Target != original) continue;          // ไม่ใช่ SO ตัวนี้

            ParameterOverride found = FindOverride(parameter.Name);
            if (found == null) continue;                         // ตัวละครนี้ไม่ได้ติ๊ก

            // copy ครั้งแรกที่เจอ override ตัว copy อยู่แค่ใน memory ไม่เขียนลงไฟล์ asset
            if (result == original)
                result = Instantiate(original);

            // ตั้งค่าลงตัว copy (ไม่ใช่ original! ไม่งั้นจะเขียนทับ asset แล้วตัวละครทุกตัวได้ค่านี้ไปด้วย)
            SetByPath(result, binding.FieldName.Split('.'), 0, found);
        }

        _resolved[original] = result;
        return result;
    }

    private ParameterOverride FindOverride(string name)
    {
        foreach (ParameterOverride parameterOverride in _overrides)
        {
            if (parameterOverride.Name == name)
                return parameterOverride;
        }

        return null;
    }

    // field อาจอยู่ใน class แม่ เลยไล่ขึ้นไปทีละชั้น (GetField ไม่หา private ของ class แม่ให้)
    private static FieldInfo FindField(Type type, string fieldName)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
                return field;
        }

        return null;
    }

    // path = ["_attackParameters", "damageAmount"], depth = ชั้นที่กำลังทำ
    // struct ได้มาเป็น "สำเนา" เลยต้องลงไปแก้ข้างใน แล้วเขียนสำเนากลับลงที่เดิม
    private static void SetByPath(object owner, string[] path, int depth, ParameterOverride found)
    {
        FieldInfo field = FindField(owner.GetType(), path[depth]);
        if (field == null)
            return;

        // ชั้นสุดท้าย: ใส่ค่าตามชนิดของ field
        if (depth == path.Length - 1)
        {
            object converted = ConvertValue(found, field.FieldType);

            // Object ผิดชนิด (เช่นลาก SO ผิดตัวมา) → ไม่ใส่ ใช้ค่าเดิมใน SO
            if (converted is UnityEngine.Object asset && !field.FieldType.IsInstanceOfType(asset))
                return;

            field.SetValue(owner, converted);
            return;
        }

        // ยังไม่ถึง: หยิบ struct ข้างในมา → ลงไปอีกชั้น → เขียนกลับ
        object inner = field.GetValue(owner);
        if (inner == null)
            return;

        SetByPath(inner, path, depth + 1, found);
        field.SetValue(owner, inner);
    }

    // หยิบค่าจากช่องที่ตรงกับชนิดของ field
    private static object ConvertValue(ParameterOverride found, Type type)
    {
        if (type == typeof(int))     return Mathf.RoundToInt(found.Value);
        if (type.IsEnum)             return Enum.ToObject(type, Mathf.RoundToInt(found.Value));
        if (type == typeof(bool))    return found.BoolValue;
        if (type == typeof(string))  return found.StringValue;
        if (type == typeof(Vector3)) return found.Vector;
        if (type == typeof(Vector2)) return (Vector2)found.Vector;
        if (type == typeof(Color))   return found.ColorValue;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return found.ObjectValue;
        return found.Value;
    }

    // ตัว copy ไม่ใช่ asset Unity ไม่ลบให้เอง ตัวละครตายก็ลบทิ้ง
    private void OnDestroy()
    {
        foreach (KeyValuePair<StateElementSO, StateElementSO> pair in _resolved)
        {
            if (pair.Value != pair.Key)
                Destroy(pair.Value);
        }

        _resolved.Clear();
    }
}

[Serializable]
public class ParameterOverride
{
    [SerializeField] private string _name;
    [SerializeField] private float _value;                 // float, int, enum
    [SerializeField] private bool _bool;
    [SerializeField] private string _string;
    [SerializeField] private Vector3 _vector;              // Vector3, Vector2
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private UnityEngine.Object _object;   // SO, AnimationClip, InputActionReference ...

    public string Name => _name;
    public float Value => _value;
    public bool BoolValue => _bool;
    public string StringValue => _string;
    public Vector3 Vector => _vector;
    public Color ColorValue => _color;
    public UnityEngine.Object ObjectValue => _object;
}
