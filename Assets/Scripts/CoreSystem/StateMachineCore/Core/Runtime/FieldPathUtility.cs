using System;
using System.Reflection;
using UnityEngine;

// ตั้งค่า field ใน object ตาม path เช่น "_attackParameters.damageAmount"
// ใช้ร่วมกันทั้งค่าของตัวละคร (ParameterOverride) และค่าของ Variant (FieldOverride)
public static class FieldPathUtility
{
    public static void Apply(object owner, string fieldPath, OverrideValue value)
    {
        SetByPath(owner, fieldPath.Split('.'), 0, value);
    }

    // field อาจประกาศไว้ใน base class ของ C# (inheritance) เลยไล่ขึ้นไปทีละชั้น
    // (GetField ไม่หา private ที่ประกาศใน base class ให้)
    private static FieldInfo FindField(Type type, string fieldName)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    // path = ["_attackParameters", "damageAmount"], depth = ชั้นที่กำลังทำ
    // struct ได้มาเป็น "สำเนา" เลยต้องลงไปแก้ข้างใน แล้วเขียนสำเนากลับลงที่เดิม
    private static void SetByPath(object owner, string[] path, int depth, OverrideValue value)
    {
        FieldInfo field = FindField(owner.GetType(), path[depth]);
        if (field == null)
        {
            return;
        }

        bool isLastPart = depth == path.Length - 1;
        if (isLastPart)
        {
            object converted = ConvertValue(value, field.FieldType);

            // Object ผิดชนิด (เช่นลาก SO ผิดตัวมา) → ไม่ใส่ ใช้ค่าเดิมใน SO
            if (converted is UnityEngine.Object asset && !field.FieldType.IsInstanceOfType(asset))
            {
                return;
            }

            field.SetValue(owner, converted);
            return;
        }

        // ยังไม่ถึง: หยิบ struct ข้างในมา → ลงไปอีกชั้น → เขียนกลับ
        object inner = field.GetValue(owner);
        if (inner == null)
        {
            return;
        }

        SetByPath(inner, path, depth + 1, value);
        field.SetValue(owner, inner);
    }

    // หยิบค่าจากช่องที่ตรงกับชนิดของ field
    private static object ConvertValue(OverrideValue value, Type type)
    {
        if (type == typeof(int))
        {
            return Mathf.RoundToInt(value.Value);
        }

        if (type.IsEnum)
        {
            return Enum.ToObject(type, Mathf.RoundToInt(value.Value));
        }

        if (type == typeof(bool))
        {
            return value.BoolValue;
        }

        if (type == typeof(string))
        {
            return value.StringValue;
        }

        if (type == typeof(Vector3))
        {
            return value.Vector;
        }

        if (type == typeof(Vector2))
        {
            return (Vector2)value.Vector;
        }

        if (type == typeof(Color))
        {
            return value.ColorValue;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            return value.ObjectValue;
        }

        // float
        return value.Value;
    }
}
