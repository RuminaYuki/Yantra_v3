#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Knokblack.Editor
{
    internal static class KnokblackGraphBuilder
    {
        private const string Root = "Assets/Knokblack0.1";
        private const string GraphPath = Root + "/Knokblack0.1.vfx";
        private const string EventName = "CastBurst";
        private const string PackagePath = "Packages/com.unity.visualeffectgraph";
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static Type[] graphTypes;

        [MenuItem("Tools/VFX/Create Knokblack0.1")]
        private static void CreateGraph()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
            if (existing != null)
            {
                SelectGraph(existing);
                return;
            }

            if (File.Exists(GraphPath) || File.Exists(GraphPath + ".meta"))
            {
                Debug.LogError("Knokblack found a file at " + GraphPath + ". Move it before creating a new graph.");
                return;
            }

            try
            {
                var editorAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
                    assembly => assembly.GetName().Name == "Unity.VisualEffectGraph.Editor")
                    ?? Assembly.Load("Unity.VisualEffectGraph.Editor");
                graphTypes = LoadTypes(editorAssembly);
                var core = LoadTexture("Core");
                var shockwave = LoadTexture("Shockwave");
                var glyph = LoadTexture("Glyph");
                CopyTemplate(editorAssembly);

                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
                if (asset == null)
                    throw new InvalidOperationException("Unity could not import the Visual Effect Graph template.");

                object resource = GetResource(asset);
                object graph = TryGet(resource, "graph");
                if (TryCall(resource, "GetOrCreateGraph", out var ownedGraph))
                    graph = ownedGraph;
                if (graph == null)
                    graph = CallExtension("GetOrCreateGraph", resource, graphTypes);

                Call(graph, "RemoveAllChildren");
                object cast = Context(graph, "VFXBasicEvent", EventName, 600, -100);
                SetMember(cast, "eventName", EventName);

                AddCore(graph, cast, core);
                AddShockwave(graph, cast, shockwave);
                AddFragments(graph, cast, glyph);
                SaveGraph(graph, resource);

                var imported = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
                var events = new List<string>();
                if (imported == null || !TryCall(imported, "GetEvents", out _, events) || !events.Contains(EventName))
                    throw new InvalidOperationException("The imported graph did not expose the CastBurst event.");

                SelectGraph(imported);
                Debug.Log("Created " + GraphPath + ". Play the CastBurst event to preview it.", imported);
            }
            catch (Exception exception)
            {
                if (File.Exists(GraphPath))
                {
                    if (!AssetDatabase.DeleteAsset(GraphPath))
                    {
                        File.Delete(GraphPath);
                        if (File.Exists(GraphPath + ".meta"))
                            File.Delete(GraphPath + ".meta");
                        AssetDatabase.Refresh();
                    }
                }

                while (exception is TargetInvocationException && exception.InnerException != null)
                    exception = exception.InnerException;
                Debug.LogError("Knokblack could not create the graph. Check that the Visual Effect Graph package " +
                    "is installed for this project.\n" + exception);
            }
            finally
            {
                graphTypes = null;
            }
        }

        private static void AddCore(object graph, object cast, Texture2D texture)
        {
            AddSystem(graph, cast, "Core", 0, 1, 0f, texture, "Alpha", out var init, out var output);
            SetAttribute(init, "lifetime", 0.14f);
            SetAttribute(init, "position", Vector3.zero);
            SetCurve(output, "size", KeyedCurve(new Keyframe(0f, 0.36f), new Keyframe(0.35f, 0.55f), new Keyframe(1f, 0.33f)));
            SetCurve(output, "alpha", KeyedCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));
        }

        private static void AddShockwave(object graph, object cast, Texture2D texture)
        {
            AddSystem(graph, cast, "Shockwave", 600, 1, 0f, texture, "Additive", out var init, out var output);
            SetAttribute(init, "lifetime", 0.38f);
            SetAttribute(init, "position", Vector3.zero);
            SetAttribute(init, "color", new Color(1.8f, 0.075f, 0.09f));
            SetCurve(output, "size", AnimationCurve.Linear(0f, 0.22f, 1f, 2.2f));
            SetCurve(output, "alpha", AnimationCurve.Linear(0f, 0.85f, 1f, 0f));

            var orient = Node("Orient");
            SetEnum(orient, "mode", "Advanced");
            SetEnum(orient, "axes", "XY");
            SetInput(orient, 0, Vector3.right);
            SetInput(orient, 1, Vector3.back);
            Call(output, "AddChild", orient);
        }

        private static void AddFragments(object graph, object cast, Texture2D texture)
        {
            AddSystem(graph, cast, "Glyph Fragments", 1200, 12, 0.03f, texture, "Additive", out var init, out var output);
            SetAttribute(init, "lifetime", 0.30f, 0.55f, "Uniform");
            SetAttribute(init, "position", Vector3.zero);
            SetAttribute(init, "velocity", new Vector3(-3.8f, -2.2f, -3.8f), new Vector3(3.8f, 2.2f, 3.8f), "PerComponent");
            SetAttribute(init, "size", 0.06f, 0.15f, "Uniform");
            SetAttribute(init, "color", new Color(2.1f, 0.28f, 0.06f));
            SetCurve(output, "alpha", AnimationCurve.Linear(0f, 1f, 1f, 0f));
        }

        private static void AddSystem(object graph, object cast, string label, float x, int count, float delay,
            Texture2D texture, string blend, out object init, out object output)
        {
            var spawner = Context(graph, "VFXBasicSpawner", label + " Spawn", x, 150);
            init = Context(graph, "VFXBasicInitialize", label + " Initialize", x, 445);
            var update = Context(graph, "VFXBasicUpdate", label + " Update", x, 1080);
            output = Context(graph, "VFXPlanarPrimitiveOutput", label + " Output", x, 1265);

            Call(cast, "LinkTo", spawner, 0, 0);
            Call(spawner, "LinkTo", init, 0, 0);
            Call(init, "LinkTo", update, 0, 0);
            Call(update, "LinkTo", output, 0, 0);

            var burst = Node("VFXSpawnerBurst");
            SetInput(burst, 0, (float)count);
            SetInput(burst, 1, delay);
            Call(spawner, "AddChild", burst);

            SetSetting(init, "capacity", 128u);
            if (!TryCall(init, "GetData", out var data))
                data = TryGet(init, "data");
            if (data != null)
                TrySetEnum(data, "boundsMode", "Manual");
            var bounds = FindInput(init, "bounds");
            if (bounds != null)
                SetSlot(bounds, new Bounds(Vector3.zero, Vector3.one * 6f));

            TrySetEnum(output, "primitiveType", "Quad");
            SetEnum(output, "blendMode", blend);
            var textureSlot = FindInput(output, "mainTexture") ?? FindInput(output, typeof(Texture2D));
            if (textureSlot == null)
                throw new InvalidOperationException("The particle output has no Main Texture slot.");
            SetSlot(textureSlot, texture);
        }

        private static void SetAttribute(object context, string attribute, object first, object second = null,
            string random = null)
        {
            object block = Node("SetAttribute");
            SetSetting(block, "attribute", attribute);
            if (random != null)
                SetEnum(block, "Random", random);
            SetInput(block, 0, first);
            if (second != null)
                SetInput(block, 1, second);
            Call(context, "AddChild", block);
        }

        private static void SetCurve(object context, string attribute, AnimationCurve curve)
        {
            object block = Node("AttributeFromCurve", "SetAttributeFromCurve");
            SetSetting(block, "attribute", attribute);
            SetEnum(block, "sampleMode", "OverLife");
            var slot = FindInput(block, typeof(AnimationCurve));
            if (slot == null)
                throw new InvalidOperationException("The " + attribute + " block has no Animation Curve slot.");
            SetSlot(slot, curve);
            Call(context, "AddChild", block);
        }

        private static AnimationCurve KeyedCurve(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int index = 0; index < keys.Length; index++)
            {
                AnimationUtility.SetKeyBroken(curve, index, true);
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        private static void CopyTemplate(Assembly editorAssembly)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(editorAssembly);
            foreach (var name in new[] { "03_Simple_Burst.vfx", "02_Simple_Loop.vfx" })
            {
                string relativePath = "Editor/Templates/" + name;
                string path = PackagePath + "/" + relativePath;
                if (AssetDatabase.LoadMainAssetAtPath(path) != null && AssetDatabase.CopyAsset(path, GraphPath))
                    return;

                if (package == null)
                    continue;
                string source = Path.Combine(package.resolvedPath, relativePath);
                if (!File.Exists(source))
                    continue;
                File.Copy(source, GraphPath);
                AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
                return;
            }
            throw new FileNotFoundException("Unity's Visual Effect Graph templates were unavailable in " + PackagePath + ".");
        }

        private static object GetResource(VisualEffectAsset asset)
        {
            if (TryCall(asset, "GetResource", out var resource) && resource != null)
                return resource;

            var relevantTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal))
                .SelectMany(LoadTypes)
                .Where(type => (type.Namespace ?? "").IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0);
            foreach (var type in graphTypes.Concat(relevantTypes))
            {
                if (TryCallStatic(type, "GetResource", asset, out resource) && resource != null)
                    return resource;
                if (type.Name == "VisualEffectResource" &&
                    TryCallStatic(type, "GetResourceAtPath", GraphPath, out resource) && resource != null)
                    return resource;
            }
            throw new MissingMethodException("Could not locate the VisualEffectResource for " + GraphPath + ".");
        }

        private static void SaveGraph(object graph, object resource)
        {
            EditorUtility.SetDirty((UnityEngine.Object)graph);
            EditorUtility.SetDirty((UnityEngine.Object)resource);
            if (!TryCall(graph, "CompileAndUpdateAsset", out _) && !TryCall(graph, "CompileAndUpdateAsset", out _, true))
                Call(graph, "RecompileIfNeeded");
            if (!TryCall(resource, "WriteAssetWithSubAssets", out _) && !TryCall(graph, "WriteAssetWithSubAssets", out _))
                AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static Texture2D LoadTexture(string name)
        {
            string path = Root + "/Textures/" + name + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new FileNotFoundException("Missing texture " + path + ". Import the complete package.");
            return texture;
        }

        private static void SelectGraph(VisualEffectAsset asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static object Context(object graph, string type, string label, float x, float y)
        {
            object context = Node(type);
            TrySetMember(context, "label", label);
            SetMember(context, "position", new Vector2(x, y));
            Call(graph, "AddChild", context);
            return context;
        }

        private static object Node(params string[] names)
        {
            foreach (var name in names)
            {
                Type type = graphTypes.FirstOrDefault(candidate => candidate.Name == name && !candidate.IsAbstract);
                if (type != null)
                    return ScriptableObject.CreateInstance(type);
            }
            throw new MissingMemberException("Visual Effect Graph node unavailable: " + string.Join(", ", names));
        }

        private static void SetInput(object model, int index, object value)
        {
            SetSlot(Call(model, "GetInputSlot", index), value);
        }

        private static object FindInput(object model, object nameOrType)
        {
            var slots = (IEnumerable)Get(model, "inputSlots");
            foreach (var slot in slots)
            {
                var property = Get(slot, "property");
                if (nameOrType is string name && SameName(Get(property, "name").ToString(), name))
                    return slot;
                if (nameOrType is Type type && type.IsAssignableFrom((Type)Get(property, "type")))
                    return slot;
            }
            return null;
        }

        private static void SetSlot(object slot, object value)
        {
            var type = (Type)Get(Get(slot, "property"), "type");
            SetMember(slot, "value", ConvertSlotValue(value, type));
        }

        private static object ConvertSlotValue(object value, Type type)
        {
            if (type.IsInstanceOfType(value))
                return value;
            if (value is Color color && type == typeof(Vector3))
                return new Vector3(color.r, color.g, color.b);
            if (value is Color rgba && type == typeof(Vector4))
                return (Vector4)rgba;
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var parameters = method.GetParameters();
                if ((method.Name == "op_Implicit" || method.Name == "op_Explicit") &&
                    method.ReturnType == type && parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(value))
                    return method.Invoke(null, new[] { value });
            }

            if (value is Vector3 vector)
            {
                var container = Activator.CreateInstance(type);
                if (TrySetMember(container, "vector", vector) || TrySetMember(container, "position", vector) ||
                    TrySetMember(container, "direction", vector) || TrySetMember(container, "value", vector))
                    return container;
            }

            if (value is Bounds bounds)
            {
                var container = Activator.CreateInstance(type);
                SetMember(container, "center", bounds.center);
                SetMember(container, "size", bounds.size);
                return container;
            }

            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        private static void SetSetting(object model, string name, object value)
        {
            Call(model, "SetSettingValue", name, value);
        }

        private static void SetEnum(object model, string setting, string choice)
        {
            if (!TrySetEnum(model, setting, choice))
                throw new MissingMemberException(model.GetType().Name + " does not expose " + setting + " = " + choice + ".");
        }

        private static bool TrySetEnum(object model, string setting, string choice)
        {
            var fields = Hierarchy(model.GetType()).SelectMany(type => type.GetFields(Members))
                .Where(field => field.FieldType.IsEnum).ToArray();
            FieldInfo field = fields.FirstOrDefault(candidate => SameName(candidate.Name, setting) && EnumChoice(candidate, choice) != null);
            if (field == null)
            {
                var matching = fields.Where(candidate => EnumChoice(candidate, choice) != null).ToArray();
                if (matching.Length != 1)
                    return false;
                field = matching[0];
            }
            SetSetting(model, field.Name, Enum.Parse(field.FieldType, EnumChoice(field, choice)));
            return true;
        }

        private static string EnumChoice(FieldInfo field, string choice)
        {
            return Enum.GetNames(field.FieldType).FirstOrDefault(name => SameName(name, choice));
        }

        private static bool SameName(string actual, string expected)
        {
            return string.Equals(actual.Replace(" ", "").Replace("_", ""),
                expected.Replace(" ", "").Replace("_", ""), StringComparison.OrdinalIgnoreCase);
        }

        private static Type[] LoadTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type != null).ToArray(); }
        }

        private static IEnumerable<Type> Hierarchy(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                yield return current;
        }

        private static object TryGet(object target, string name)
        {
            foreach (var type in Hierarchy(target.GetType()))
            {
                var property = type.GetProperty(name, Members);
                if (property != null)
                    return property.GetValue(target);
                var field = type.GetField(name, Members);
                if (field != null)
                    return field.GetValue(target);
            }
            return null;
        }

        private static object Get(object target, string name)
        {
            return TryGet(target, name) ?? throw new MissingMemberException(target.GetType().Name, name);
        }

        private static void SetMember(object target, string name, object value)
        {
            if (!TrySetMember(target, name, value))
                throw new MissingMemberException(target.GetType().Name, name);
        }

        private static bool TrySetMember(object target, string name, object value)
        {
            foreach (var type in Hierarchy(target.GetType()))
            {
                var property = type.GetProperty(name, Members);
                if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
                {
                    property.SetValue(target, value);
                    return true;
                }
                var field = type.GetField(name, Members);
                if (field != null && field.FieldType.IsInstanceOfType(value))
                {
                    field.SetValue(target, value);
                    return true;
                }
            }
            return false;
        }

        private static object CallExtension(string name, object target, IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                if (TryCallStatic(type, name, target, out var result) && result != null)
                    return result;
            }
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static object Call(object target, string name, params object[] arguments)
        {
            if (TryCall(target, name, out var result, arguments))
                return result;
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static bool TryCall(object target, string name, out object result, params object[] arguments)
        {
            return Invoke(Hierarchy(target.GetType()).SelectMany(type => type.GetMethods(Members)), target, name, arguments, out result);
        }

        private static bool TryCallStatic(Type type, string name, object argument, out object result)
        {
            return Invoke(type.GetMethods(Members), null, name, new[] { argument }, out result);
        }

        private static bool Invoke(IEnumerable<MethodInfo> methods, object target, string name, object[] arguments, out object result)
        {
            foreach (var method in methods)
            {
                if (method.Name != name || method.ContainsGenericParameters || method.IsStatic != (target == null))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length < arguments.Length)
                    continue;
                var values = new object[parameters.Length];
                int index = 0;
                for (; index < arguments.Length; index++)
                {
                    if (!parameters[index].ParameterType.IsInstanceOfType(arguments[index]))
                        break;
                    values[index] = arguments[index];
                }
                if (index != arguments.Length)
                    continue;
                for (; index < parameters.Length && parameters[index].IsOptional; index++)
                    values[index] = parameters[index].DefaultValue;
                if (index != parameters.Length)
                    continue;
                result = method.Invoke(target, values);
                return true;
            }
            result = null;
            return false;
        }
    }
}
#endif
