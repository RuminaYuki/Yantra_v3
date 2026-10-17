using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

/// <summary>
/// ONE-TIME tool (moving off the old StatsParameter modifiers). Delete this file afterwards.
/// Select a Transition Table file AND its CharacterStatsSO together in the Project window (Ctrl-click),
/// e.g. PKa_TransitionTable + PKa_Stats, then run it. Best after Localize Selected Table.
/// For each modifier in the stats list:
///   - a Header modifier's label becomes the category of the parameters after it
///   - each value the modifier sets (not its gizmo settings) becomes a parameter in the table's
///     Blackboard, bound to the same field on the modifier's target, in the category
///   - the target's field is set to the modifier's value, so the default is what the stats gave
/// The target is found in the table's file when it was copied in (same type, same name without the
/// character's prefix); otherwise the target itself is used if the table uses it.
/// Modifiers that target a component (e.g. Max Health) or values with no [Exposed] field to land on
/// are skipped and listed in the Console. Existing parameters with the same name are left alone.
/// </summary>
public static class StatsToParametersTool
{
    private const string BlackboardName = "Exposed_Parameters";
    private const string TableLinkProperty = "_exposedParameters";

    [MenuItem("Tools/State Machine/Stats To Parameters (select table + stats)")]
    private static void Run()
    {
        // The files picked in the Project window, read by path (their main assets).
        List<Object> selected = Selection.assetGUIDs
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(AssetDatabase.LoadMainAssetAtPath)
            .Where(asset => asset != null)
            .ToList();

        TransitionTableSO table = selected.OfType<TransitionTableSO>().FirstOrDefault();

        // A stats list: anything with a _modifiers list (CharacterStatsSO).
        ScriptableObject stats = selected.OfType<ScriptableObject>()
            .FirstOrDefault(asset => !(asset is TransitionTableSO) &&
                                     new SerializedObject(asset).FindProperty("_modifiers") != null);

        if (table == null || stats == null)
        {
            string seen = selected.Count == 0
                ? "(nothing)"
                : string.Join("\n", selected.Select(asset => $"  {asset.name} ({asset.GetType().Name})"));
            EditorUtility.DisplayDialog(
                "Stats To Parameters",
                "Select the Transition Table file and the CharacterStatsSO together (Ctrl-click both).\n\n" +
                $"Selected now:\n{seen}",
                "OK");
            return;
        }

        string tablePath = AssetDatabase.GetAssetPath(table);
        string prefix = PrefixOf(table.name);
        List<Plan> plans = MakePlans(stats, table, tablePath, prefix, out List<string> skipped);

        var summary = new StringBuilder();
        foreach (IGrouping<string, Plan> category in plans.GroupBy(plan => plan.Category))
        {
            summary.AppendLine(category.Key.Length > 0 ? category.Key : "(Uncategorized)");
            foreach (Plan plan in category)
            {
                string targets = string.Join(", ", plan.Targets.Select(target => target.name));
                summary.AppendLine($"   {plan.Name}  →  {targets} . {plan.FieldPath}");
            }
        }

        if (skipped.Count > 0)
        {
            summary.AppendLine($"\nSkipped {skipped.Count} (see the Console after running).");
        }

        if (!EditorUtility.DisplayDialog(
                "Stats To Parameters",
                $"{stats.name} → {table.name}: {plans.Count} parameter(s)\n\n{summary}\n" +
                "The targets' values are set to the stats' values. This can't be undone; commit first.",
                "Create", "Cancel"))
        {
            return;
        }

        ExposedParametersSO blackboard = FindOrCreateBlackboard(table, tablePath);
        int added = 0;
        var blackboardObject = new SerializedObject(blackboard);
        SerializedProperty parameters = blackboardObject.FindProperty("_parameters");
        var existingNames = new HashSet<string>(blackboard.Parameters.Select(parameter => parameter.Name));

        foreach (Plan plan in plans)
        {
            foreach (ScriptableObject target in plan.Targets)
            {
                CopyValue(plan.Source, target, plan.FieldPath);
            }

            if (existingNames.Contains(plan.Name))
            {
                skipped.Add($"{plan.Name}: a parameter with this name already exists (value still copied)");
                continue;
            }

            parameters.arraySize++;
            SerializedProperty parameter = parameters.GetArrayElementAtIndex(parameters.arraySize - 1);
            parameter.FindPropertyRelative("_name").stringValue = plan.Name;
            parameter.FindPropertyRelative("_category").stringValue = plan.Category;

            // One binding per target, like a VFX property wired to several nodes.
            SerializedProperty bindings = parameter.FindPropertyRelative("_bindings");
            bindings.arraySize = plan.Targets.Count;
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("_target").objectReferenceValue = plan.Targets[i];
                binding.FindPropertyRelative("_fieldName").stringValue = plan.FieldPath;
            }

            existingNames.Add(plan.Name);
            added++;
        }

        blackboardObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(blackboard);
        AssetDatabase.SaveAssets();

        var log = new StringBuilder($"[StatsToParametersTool] {table.name}: added {added} parameter(s) from {stats.name}.");
        foreach (string line in skipped)
        {
            log.Append("\n  skipped: ").Append(line);
        }

        Debug.Log(log.ToString(), table);
    }

    // ---------- Planning ----------

    private class Plan
    {
        public string Name;
        public string Category;
        public List<ScriptableObject> Targets;   // usually one; a modifier with a _target list gives several
        public string FieldPath;            // on the targets: "_distance", "_ringParameters.radius"
        public SerializedProperty Source;   // the modifier's value
    }

    private static List<Plan> MakePlans(
        ScriptableObject stats,
        TransitionTableSO table,
        string tablePath,
        string prefix,
        out List<string> skipped)
    {
        var plans = new List<Plan>();
        skipped = new List<string>();
        string category = string.Empty;

        var used = new HashSet<ScriptableObject>(UsedByFile(tablePath));
        SerializedProperty modifiers = new SerializedObject(stats).FindProperty("_modifiers");

        for (int i = 0; i < modifiers.arraySize; i++)
        {
            if (modifiers.GetArrayElementAtIndex(i).objectReferenceValue is not ScriptableObject modifier)
            {
                continue;
            }

            var serializedModifier = new SerializedObject(modifier);

            SerializedProperty label = serializedModifier.FindProperty("_label");
            if (modifier.GetType().Name == "HeaderModifierSO" && label != null)
            {
                category = label.stringValue;
                continue;
            }

            // _target is one SO, or a list of them (e.g. Set Delayed Condition).
            var originals = new List<ScriptableObject>();
            SerializedProperty targetProperty = serializedModifier.FindProperty("_target");
            if (targetProperty != null && targetProperty.isArray)
            {
                for (int t = 0; t < targetProperty.arraySize; t++)
                {
                    if (targetProperty.GetArrayElementAtIndex(t).objectReferenceValue is ScriptableObject element)
                    {
                        originals.Add(element);
                    }
                }
            }
            else if (targetProperty?.propertyType == SerializedPropertyType.ObjectReference &&
                     targetProperty.objectReferenceValue is ScriptableObject single)
            {
                originals.Add(single);
            }

            if (originals.Count == 0)
            {
                skipped.Add($"{modifier.name}: no SO target (a component value? use a prefab variant)");
                continue;
            }

            var targets = new List<ScriptableObject>();
            foreach (ScriptableObject original in originals)
            {
                ScriptableObject resolved = ResolveTarget(original, tablePath, prefix, used);
                if (resolved == null)
                {
                    skipped.Add($"{modifier.name}: its target {original.name} isn't used by {table.name}");
                }
                else if (!targets.Contains(resolved))
                {
                    targets.Add(resolved);
                }
            }

            if (targets.Count == 0)
            {
                continue;
            }

            List<(string Path, SerializedProperty Value)> values = ValuesOf(serializedModifier);
            List<string> targetPaths = ExposedPaths(targets[0].GetType());
            string baseName = WithoutSuffix(WithoutPrefix(modifier.name, prefix));

            foreach ((string path, SerializedProperty value) in values)
            {
                string leaf = Normalize(path.Substring(path.LastIndexOf('.') + 1));
                string targetPath = targetPaths.FirstOrDefault(candidate =>
                    Normalize(candidate.Substring(candidate.LastIndexOf('.') + 1)) == leaf);

                if (targetPath == null)
                {
                    skipped.Add($"{modifier.name}.{path}: {targets[0].GetType().Name} has no [Exposed] field for it");
                    continue;
                }

                plans.Add(new Plan
                {
                    Name = values.Count == 1 ? baseName : $"{baseName}_{Title(leaf, path)}",
                    Category = category,
                    Targets = targets,
                    FieldPath = targetPath,
                    Source = value
                });
            }
        }

        return plans;
    }

    // The copy of `original` in the table's file (same type, its name without the prefix), or
    // `original` itself when the table (or a table in its file) uses it.
    private static ScriptableObject ResolveTarget(
        ScriptableObject original,
        string tablePath,
        string prefix,
        HashSet<ScriptableObject> used)
    {
        if (AssetDatabase.GetAssetPath(original) == tablePath)
        {
            return original;
        }

        string localName = WithoutPrefix(original.name, prefix);
        ScriptableObject copy = AssetDatabase.LoadAllAssetsAtPath(tablePath)
            .OfType<ScriptableObject>()
            .FirstOrDefault(asset => asset.GetType() == original.GetType() &&
                                     (asset.name == localName || asset.name == original.name));
        if (copy != null)
        {
            return copy;
        }

        return used.Contains(original) ? original : null;
    }

    // Everything the tables in the file use, through their fields (not into other tables' files).
    private static IEnumerable<ScriptableObject> UsedByFile(string tablePath)
    {
        var visited = new HashSet<ScriptableObject>();
        var pending = new Stack<ScriptableObject>(
            AssetDatabase.LoadAllAssetsAtPath(tablePath).OfType<TransitionTableSO>());

        while (pending.Count > 0)
        {
            ScriptableObject current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            SerializedProperty property = new SerializedObject(current).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue is ScriptableObject child &&
                    (child is StateSO || child is StateActionSO || child is StateConditionSO || child is TransitionTableSO))
                {
                    pending.Push(child);
                }
            }
        }

        return visited;
    }

    // The values a modifier sets: number / vector leaves, without its target, anchor and gizmo settings.
    private static List<(string Path, SerializedProperty Value)> ValuesOf(SerializedObject modifier)
    {
        var values = new List<(string, SerializedProperty)>();
        SerializedProperty property = modifier.GetIterator();
        property.Next(true);

        do
        {
            string path = property.propertyPath;
            if (path.StartsWith("m_", StringComparison.Ordinal) ||
                path.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                    values.Add((path, property.Copy()));
                    break;
            }
        }
        while (property.Next(property.propertyType == SerializedPropertyType.Generic &&
                             !property.isArray &&
                             property.propertyPath.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) < 0));

        return values;
    }

    // Paths the Blackboard can bind on a type: each [Exposed] field, and the number fields inside an
    // [Exposed] struct ("_ringParameters.radius").
    private static List<string> ExposedPaths(Type type)
    {
        var paths = new List<string>();
        for (Type current = type; current != null && current != typeof(ScriptableObject); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                          BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.GetCustomAttribute<ExposedAttribute>() == null)
                {
                    continue;
                }

                Type fieldType = field.FieldType;
                bool isStruct = fieldType.IsValueType && !fieldType.IsPrimitive && !fieldType.IsEnum &&
                                fieldType != typeof(Vector2) && fieldType != typeof(Vector3) &&
                                fieldType != typeof(Color);
                if (!isStruct)
                {
                    paths.Add(field.Name);
                    continue;
                }

                foreach (FieldInfo inner in fieldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    paths.Add($"{field.Name}.{inner.Name}");
                }
            }
        }

        return paths;
    }

    private static void CopyValue(SerializedProperty source, ScriptableObject target, string targetPath)
    {
        var serializedTarget = new SerializedObject(target);
        SerializedProperty destination = serializedTarget.FindProperty(targetPath);
        if (destination == null)
        {
            return;
        }

        switch (destination.propertyType)
        {
            case SerializedPropertyType.Float:
                destination.floatValue = source.propertyType == SerializedPropertyType.Integer ? source.intValue : source.floatValue;
                break;
            case SerializedPropertyType.Integer:
                destination.intValue = source.propertyType == SerializedPropertyType.Float
                    ? Mathf.RoundToInt(source.floatValue)
                    : source.intValue;
                break;
            case SerializedPropertyType.Vector3:
                destination.vector3Value = source.propertyType == SerializedPropertyType.Vector2 ? source.vector2Value : source.vector3Value;
                break;
            case SerializedPropertyType.Vector2:
                destination.vector2Value = source.propertyType == SerializedPropertyType.Vector3 ? (Vector2)source.vector3Value : source.vector2Value;
                break;
        }

        serializedTarget.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    // ---------- Blackboard ----------

    // The file's Blackboard, made if missing, and every table in the file linked to it (like the
    // Parameters panel does when a table is opened).
    private static ExposedParametersSO FindOrCreateBlackboard(TransitionTableSO table, string tablePath)
    {
        ExposedParametersSO blackboard = AssetDatabase.LoadAllAssetsAtPath(tablePath)
            .OfType<ExposedParametersSO>()
            .FirstOrDefault();

        if (blackboard == null)
        {
            blackboard = ScriptableObject.CreateInstance<ExposedParametersSO>();
            blackboard.name = BlackboardName;
            AssetDatabase.AddObjectToAsset(blackboard, table);
        }

        foreach (TransitionTableSO fileTable in AssetDatabase.LoadAllAssetsAtPath(tablePath).OfType<TransitionTableSO>())
        {
            var serializedTable = new SerializedObject(fileTable);
            SerializedProperty link = serializedTable.FindProperty(TableLinkProperty);
            if (link != null && link.objectReferenceValue != blackboard)
            {
                link.objectReferenceValue = blackboard;
                serializedTable.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        return blackboard;
    }

    // ---------- Names ----------

    // "PKa_TransitionTable" -> "PKa", "PlayerAction_TransitionTable" -> "Player".
    private static string PrefixOf(string tableName)
    {
        string stem = tableName.Split('_')[0];
        for (int i = 1; i < stem.Length; i++)
        {
            if (char.IsLower(stem[i - 1]) && char.IsUpper(stem[i]))
            {
                return stem.Substring(0, i);
            }
        }

        return stem;
    }

    private static string WithoutPrefix(string name, string prefix)
    {
        return prefix.Length > 0 &&
               name.Length > prefix.Length &&
               name.StartsWith(prefix, StringComparison.Ordinal) &&
               char.IsUpper(name[prefix.Length])
            ? name.Substring(prefix.Length)
            : name;
    }

    // "ChaseRange_Modifier" -> "ChaseRange", "PatrolSetRanPosNav_ModifierSO" -> "PatrolSetRanPosNav".
    private static string WithoutSuffix(string name)
    {
        int cut = name.IndexOf("_Modifier", StringComparison.Ordinal);
        return cut > 0 ? name.Substring(0, cut) : name;
    }

    // "_minDuration" / "minDuration" -> "minduration", to match a modifier field with a target field.
    private static string Normalize(string fieldName)
    {
        return fieldName.TrimStart('_').ToLowerInvariant();
    }

    // "_minDuration" -> "MinDuration" (from the original spelling, not the normalized one).
    private static string Title(string normalized, string path)
    {
        string leaf = path.Substring(path.LastIndexOf('.') + 1).TrimStart('_');
        return leaf.Length == 0 ? normalized : char.ToUpperInvariant(leaf[0]) + leaf.Substring(1);
    }
}
