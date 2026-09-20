using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Scaffolds a new IStatModifier ScriptableObject script from a small template, so each new
// modifier only needs its fields typed once here instead of hand-copying the boilerplate
// (usings, [CreateAssetMenu], class declaration, IStatModifier implementation) every time.
// The Apply() body is left as a TODO since that part is unique per modifier and can't be
// generated automatically.
public class StatModifierScriptCreatorWindow : EditorWindow
{
    private class FieldEntry
    {
        public string type = "float";
        public string name = "_value";
    }

    private string _className = "SetMaxHealthModifierSO";
    private string _fileName = "NewModifier";
    private string _menuSuffix = "Set Max Health";
    private DefaultAsset _destinationFolder;
    private readonly List<FieldEntry> _fields = new List<FieldEntry> { new FieldEntry() };

    [MenuItem("Tools/State Machine/Create Stat Modifier Script")]
    private static void Open()
    {
        GetWindow<StatModifierScriptCreatorWindow>("Create Stat Modifier");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Generates a ScriptableObject class implementing IStatModifier with the fields " +
            "listed below. The Apply() method body is left as a TODO for you to fill in.",
            MessageType.Info);

        EditorGUILayout.Space();
        _className = EditorGUILayout.TextField("Class Name", _className);
        _fileName = EditorGUILayout.TextField("CreateAssetMenu File Name", _fileName);
        _menuSuffix = EditorGUILayout.TextField("CreateAssetMenu Suffix", _menuSuffix);
        _destinationFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Destination Folder", _destinationFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);

        for (int i = 0; i < _fields.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _fields[i].type = EditorGUILayout.TextField(_fields[i].type, GUILayout.Width(180));
            _fields[i].name = EditorGUILayout.TextField(_fields[i].name);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                _fields.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Field"))
            _fields.Add(new FieldEntry());

        EditorGUILayout.Space();
        bool canCreate = !string.IsNullOrEmpty(_className) &&
                          _destinationFolder != null &&
                          AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_destinationFolder));

        GUI.enabled = canCreate;
        if (GUILayout.Button("Create Script", GUILayout.Height(28)))
            CreateScript();
        GUI.enabled = true;
    }

    private void CreateScript()
    {
        string folderPath = AssetDatabase.GetAssetPath(_destinationFolder);
        string filePath = $"{folderPath}/{_className}.cs";

        if (File.Exists(filePath))
        {
            Debug.LogError($"[StatModifierScriptCreator] '{filePath}' already exists.");
            return;
        }

        File.WriteAllText(filePath, BuildScript());
        AssetDatabase.Refresh();

        Debug.Log($"[StatModifierScriptCreator] Created '{filePath}'.");
    }

    private string BuildScript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"[CreateAssetMenu(fileName = \"{_fileName}\",");
        sb.AppendLine($"    menuName = \"YUKI Learning State Machine/StatsParameter/Modifiers/{_menuSuffix}\")]");
        sb.AppendLine($"public class {_className} : ScriptableObject, IStatModifier");
        sb.AppendLine("{");

        foreach (FieldEntry field in _fields)
            sb.AppendLine($"    [SerializeField] private {field.type} {field.name};");

        sb.AppendLine();
        sb.AppendLine("    public void Apply()");
        sb.AppendLine("    {");
        sb.AppendLine("        // TODO: implement");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
