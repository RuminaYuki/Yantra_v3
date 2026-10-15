#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem.GraphEditor
{
    // Shows the objective id as a dropdown of the quest's objectives.
    [CustomEditor(typeof(QuestObjectiveReporter))]
    public class QuestObjectiveReporterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var questProp = serializedObject.FindProperty("quest");
            var idProp = serializedObject.FindProperty("objectiveId");

            EditorGUILayout.PropertyField(questProp);

            var quest = questProp.objectReferenceValue as QuestSO;
            if (quest != null && quest.Objectives.Count > 0)
            {
                var names = new string[quest.Objectives.Count];
                int current = -1;
                for (int i = 0; i < names.Length; i++)
                {
                    var o = quest.Objectives[i];
                    names[i] = string.IsNullOrEmpty(o.description) ? o.id : $"{o.id}  ({o.description})";
                    if (o.id == idProp.stringValue) current = i;
                }

                if (current < 0 && !string.IsNullOrEmpty(idProp.stringValue))
                    EditorGUILayout.HelpBox($"'{idProp.stringValue}' is not an objective of this quest.", MessageType.Warning);

                int picked = EditorGUILayout.Popup("Objective", Mathf.Max(current, 0), names);
                if (picked != current) idProp.stringValue = quest.Objectives[picked].id;
            }
            else
            {
                EditorGUILayout.PropertyField(idProp);
                if (quest != null)
                    EditorGUILayout.HelpBox("This quest has no objectives. Use CompleteQuest() instead of Report().", MessageType.Info);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("amount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("oncePerRound"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
