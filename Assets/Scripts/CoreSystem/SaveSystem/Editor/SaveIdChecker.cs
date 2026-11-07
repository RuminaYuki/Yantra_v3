using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Save System > Check Save Ids
// Scans every open scene and lists:
//   - SaveId components that share the same id
//   - Saveables whose SaveKey collides (save sections are keyed by SaveKey only, not per scene)
//   - Saveables with no key (missing SaveId or empty id) - they are never saved
public class SaveIdChecker : EditorWindow
{
    private class Issue
    {
        public string title;
        public List<Object> objects = new List<Object>();
        public bool canFix; // duplicate SaveIds: every copy except the first gets a new id
    }

    private readonly List<Issue> issues = new List<Issue>();
    private Vector2 scroll;
    private int saveIdCount, saveableCount;
    private bool scanned;

    [MenuItem("Tools/Save System/Check Save Ids")]
    private static void Open()
    {
        var window = GetWindow<SaveIdChecker>("Save Id Checker");
        window.Scan();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan Open Scenes", GUILayout.Height(24))) Scan();
        GUI.enabled = issues.Any(i => i.canFix);
        if (GUILayout.Button("Fix Duplicate SaveIds", GUILayout.Height(24))) FixAll();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (!scanned) return;

        EditorGUILayout.HelpBox(
            $"SaveId: {saveIdCount}   Saveables: {saveableCount}   Problems: {issues.Count}",
            issues.Count == 0 ? MessageType.Info : MessageType.Warning);

        if (issues.Count == 0)
        {
            EditorGUILayout.LabelField("No duplicate ids found.");
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Issue issue in issues)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(issue.title, EditorStyles.boldLabel);
            foreach (Object obj in issue.objects)
            {
                EditorGUILayout.BeginHorizontal();
                if (obj == null)
                {
                    EditorGUILayout.LabelField("(deleted)");
                }
                else
                {
                    var comp = obj as Component;
                    string label = comp != null
                        ? $"{GetPath(comp.transform)}  [{comp.GetType().Name}]  ({comp.gameObject.scene.name})"
                        : obj.name;
                    EditorGUILayout.LabelField(label);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = comp != null ? comp.gameObject : obj;
                        EditorGUIUtility.PingObject(Selection.activeObject);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            if (issue.canFix && GUILayout.Button("Give new ids to copies (keep first)"))
            {
                Fix(issue);
                Scan();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        issues.Clear();
        scanned = true;

        var saveIds = new List<SaveId>();
        var saveables = new List<MonoBehaviour>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                saveIds.AddRange(root.GetComponentsInChildren<SaveId>(true));
                saveables.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m is ISaveable));
            }
        }
        saveIdCount = saveIds.Count;
        saveableCount = saveables.Count;

        // 1) Same SaveId on different objects.
        foreach (var group in saveIds.Where(s => s.HasId).GroupBy(s => s.Id).Where(g => g.Count() > 1))
        {
            var issue = new Issue { title = $"Duplicate SaveId: {group.Key}", canFix = true };
            issue.objects.AddRange(group.OrderBy(s => GetPath(s.transform)));
            issues.Add(issue);
        }

        // 2) SaveId with an empty id.
        var empty = saveIds.Where(s => !s.HasId).ToList();
        if (empty.Count > 0)
        {
            var issue = new Issue { title = "SaveId with empty id" };
            issue.objects.AddRange(empty);
            issues.Add(issue);
        }

        // 3) Saveable keys that collide. Skip groups already reported in (1) to avoid noise.
        var reportedIds = new HashSet<string>(saveIds.Where(s => s.HasId).GroupBy(s => s.Id)
            .Where(g => g.Count() > 1).Select(g => g.Key));
        var noKey = new List<Object>();
        var byKey = new Dictionary<string, List<MonoBehaviour>>();
        foreach (MonoBehaviour m in saveables)
        {
            string key = ((ISaveable)m).SaveKey;
            if (string.IsNullOrEmpty(key)) { noKey.Add(m); continue; }
            if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = new List<MonoBehaviour>();
            list.Add(m);
        }
        foreach (var pair in byKey.Where(p => p.Value.Count > 1 && !reportedIds.Contains(p.Key)))
        {
            bool sameObject = pair.Value.Select(m => m.gameObject).Distinct().Count() == 1;
            var issue = new Issue
            {
                title = sameObject
                    ? $"Saveables on one object share SaveKey: {pair.Key}"
                    : $"Duplicate SaveKey: {pair.Key}"
            };
            issue.objects.AddRange(pair.Value);
            issues.Add(issue);
        }

        // 4) Saveables with no key are skipped by SaveManager.
        if (noKey.Count > 0)
        {
            var issue = new Issue { title = "Saveable with no key (add SaveId or a fixed key)" };
            issue.objects.AddRange(noKey);
            issues.Add(issue);
        }

        Repaint();
    }

    private void FixAll()
    {
        foreach (Issue issue in issues.Where(i => i.canFix)) Fix(issue);
        Scan();
    }

    private static void Fix(Issue issue)
    {
        foreach (SaveId saveId in issue.objects.Skip(1).OfType<SaveId>())
        {
            var so = new SerializedObject(saveId);
            so.FindProperty("id").stringValue = System.Guid.NewGuid().ToString("N");
            so.ApplyModifiedProperties(); // records undo, marks dirty, keeps prefab overrides
            EditorSceneManager.MarkSceneDirty(saveId.gameObject.scene);
        }
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
