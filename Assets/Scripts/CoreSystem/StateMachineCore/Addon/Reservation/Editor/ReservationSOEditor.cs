using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ReservationSO))]
public class ReservationSOEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var so = (ReservationSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Has Any", so.HasAny);
            EditorGUILayout.IntField("Count", so.Count);
        }
    }
}
