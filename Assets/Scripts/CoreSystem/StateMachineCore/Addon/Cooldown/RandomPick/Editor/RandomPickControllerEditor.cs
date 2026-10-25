using UnityEditor;
using UnityEngine;

// Shows each option's real chance under the pools, since Weight is only a share against the others.
// Edit Mode: as if every option were ready. Play Mode: only the options whose cooldown is ready now.
[CustomEditor(typeof(RandomPickController))]
public class RandomPickControllerEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var picker = (RandomPickController)target;
        CooldownController cooldowns = Application.isPlaying ? picker.GetComponent<CooldownController>() : null;
        SerializedProperty pools = serializedObject.FindProperty("_pools");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            Application.isPlaying ? "Chance (ready now)" : "Chance (all ready)",
            EditorStyles.boldLabel);

        for (int p = 0; p < pools.arraySize; p++)
        {
            SerializedProperty pool = pools.GetArrayElementAtIndex(p);
            var poolId = pool.FindPropertyRelative("Id").objectReferenceValue as RandomPoolSO;
            DrawPool(pool, poolId, p, picker, cooldowns);
        }
    }

    private static void DrawPool(
        SerializedProperty pool,
        RandomPoolSO poolId,
        int poolIndex,
        RandomPickController picker,
        CooldownController cooldowns)
    {
        SerializedProperty options = pool.FindPropertyRelative("Options");

        float totalWeight = 0f;
        for (int i = 0; i < options.arraySize; i++)
        {
            totalWeight += WeightIfPickable(options.GetArrayElementAtIndex(i), cooldowns);
        }

        EditorGUILayout.LabelField(poolId != null ? poolId.name : $"Pool {poolIndex} (no Id)", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;

        for (int i = 0; i < options.arraySize; i++)
        {
            SerializedProperty entry = options.GetArrayElementAtIndex(i);
            Object option = entry.FindPropertyRelative("Option").objectReferenceValue;
            var cooldown = entry.FindPropertyRelative("Cooldown").objectReferenceValue as CooldownSO;
            float weight = WeightIfPickable(entry, cooldowns);

            string chance = option == null ? "no option"
                : weight > 0f ? $"{weight / totalWeight * 100f:0.#}%"
                : cooldowns != null && cooldown != null && !cooldowns.IsReady(cooldown)
                    ? $"cooldown {cooldowns.GetRemaining(cooldown):0.0}s"
                : "0%";

            EditorGUILayout.LabelField(option != null ? option.name : $"Element {i}", chance);
        }

        if (totalWeight <= 0f)
        {
            EditorGUILayout.HelpBox(
                "Nothing can be picked: a roll gives null, so the state machine needs a way out for that.",
                MessageType.Warning);
        }

        if (Application.isPlaying)
        {
            RandomOptionSO current = picker.GetCurrent(poolId);
            EditorGUILayout.LabelField("Current", current != null ? current.name : "None");
        }

        EditorGUI.indentLevel--;
    }

    // Its weight if a roll could pick it right now, else 0.
    private static float WeightIfPickable(SerializedProperty entry, CooldownController cooldowns)
    {
        Object option = entry.FindPropertyRelative("Option").objectReferenceValue;
        var cooldown = entry.FindPropertyRelative("Cooldown").objectReferenceValue as CooldownSO;
        float weight = entry.FindPropertyRelative("Weight").floatValue;

        if (option == null || weight <= 0f)
        {
            return 0f;
        }

        return cooldowns == null || cooldown == null || cooldowns.IsReady(cooldown) ? weight : 0f;
    }
}
