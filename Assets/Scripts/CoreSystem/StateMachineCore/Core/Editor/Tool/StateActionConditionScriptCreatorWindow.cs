using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Scaffolds a new StateActionSO/StateConditionSO pair (the SO "data" class plus its paired
// runtime StateAction/Condition class) from a small template, matching the existing pattern
// used throughout the project (e.g. DistanceConditionSO+DistanceCondition,
// PlayAnimatorStateActionSO+PlayAnimatorStateAction) — one file, two classes, fields flow
// from the SO's constructor call into the runtime class's constructor.
// OnUpdate()/Statement() are left as TODOs since that logic is unique per Action/Condition.
public class StateActionConditionScriptCreatorWindow : EditorWindow
{
    private enum Kind { Action, Condition }

    private class FieldEntry
    {
        public string type = "float";
        public string name = "_value";
    }

    private Kind _kind = Kind.Action;
    private string _className = "MyExample";
    private string _menuSuffix = "Standard/MyExample";
    private DefaultAsset _destinationFolder;
    private readonly List<FieldEntry> _fields = new List<FieldEntry> { new FieldEntry() };

    [MenuItem("Tools/State Machine/Create Action Or Condition Script")]
    private static void Open()
    {
        GetWindow<StateActionConditionScriptCreatorWindow>("Create Action/Condition");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Generates an SO class (StateActionSO or StateConditionSO) paired with its runtime " +
            "class (StateAction or Condition), wired together the same way as the project's " +
            "existing Actions/Conditions. OnUpdate()/Statement() are left as TODOs.",
            MessageType.Info);

        EditorGUILayout.Space();
        _kind = (Kind)EditorGUILayout.EnumPopup("Kind", _kind);
        _className = EditorGUILayout.TextField("Base Class Name", _className);
        EditorGUILayout.LabelField(" ", $"→ generates {_className}SO and {_className}");
        _menuSuffix = EditorGUILayout.TextField("CreateAssetMenu Path Suffix", _menuSuffix);
        _destinationFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Destination Folder", _destinationFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fields (become SO fields, constructor params, and runtime fields)",
            EditorStyles.boldLabel);

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
        string soClassName = _className + "SO";
        string filePath = $"{folderPath}/{soClassName}.cs";

        if (File.Exists(filePath))
        {
            Debug.LogError($"[StateActionConditionScriptCreator] '{filePath}' already exists.");
            return;
        }

        string content = _kind == Kind.Action ? BuildActionScript(soClassName) : BuildConditionScript(soClassName);
        File.WriteAllText(filePath, content);
        AssetDatabase.Refresh();

        Debug.Log($"[StateActionConditionScriptCreator] Created '{filePath}'.");
    }

    private static string ParamName(string fieldName)
    {
        return fieldName.StartsWith("_") ? fieldName.Substring(1) : fieldName;
    }

    private string BuildActionScript(string soClassName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using Yuki.Learning.StateMachine;");
        sb.AppendLine("using Yuki.Learning.StateMachine.ScriptableObjects;");
        sb.AppendLine();
        sb.AppendLine("[CreateAssetMenu(");
        sb.AppendLine($"    fileName = \"New{_className}_Action\",");
        sb.AppendLine($"    menuName = \"YUKI Learning State Machine/StateMachineList/Actions/{_menuSuffix}\")]");
        sb.AppendLine($"public class {soClassName} : StateActionSO");
        sb.AppendLine("{");

        foreach (FieldEntry field in _fields)
            sb.AppendLine($"    [SerializeField] private {field.type} {field.name};");

        sb.AppendLine();
        sb.AppendLine("    public override StateAction CreateAction(StateMachine stateMachine)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {_className}(");
        for (int i = 0; i < _fields.Count; i++)
            sb.AppendLine($"            {_fields[i].name}{(i < _fields.Count - 1 ? "," : ");")}");
        if (_fields.Count == 0)
            sb.AppendLine("            );");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"public class {_className} : StateAction");
        sb.AppendLine("{");

        foreach (FieldEntry field in _fields)
            sb.AppendLine($"    private readonly {field.type} {field.name};");

        sb.AppendLine();
        sb.Append($"    public {_className}(");
        for (int i = 0; i < _fields.Count; i++)
            sb.Append($"{_fields[i].type} {ParamName(_fields[i].name)}{(i < _fields.Count - 1 ? ", " : "")}");
        sb.AppendLine(")");
        sb.AppendLine("    {");
        foreach (FieldEntry field in _fields)
            sb.AppendLine($"        this.{field.name} = {ParamName(field.name)};");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void OnUpdate()");
        sb.AppendLine("    {");
        sb.AppendLine("        // TODO: implement");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string BuildConditionScript(string soClassName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using Yuki.Learning.StateMachine;");
        sb.AppendLine("using Yuki.Learning.StateMachine.ScriptableObjects;");
        sb.AppendLine();
        sb.AppendLine("[CreateAssetMenu(");
        sb.AppendLine($"    fileName = \"New{_className}_Condition\",");
        sb.AppendLine($"    menuName = \"YUKI Learning State Machine/StateMachineList/Conditions/{_menuSuffix}\")]");
        sb.AppendLine($"public class {soClassName} : StateConditionSO");
        sb.AppendLine("{");

        foreach (FieldEntry field in _fields)
            sb.AppendLine($"    [SerializeField] private {field.type} {field.name};");

        sb.AppendLine();
        sb.AppendLine("    public override Condition CreateCondition()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {_className}(");
        for (int i = 0; i < _fields.Count; i++)
            sb.AppendLine($"            {_fields[i].name}{(i < _fields.Count - 1 ? "," : ");")}");
        if (_fields.Count == 0)
            sb.AppendLine("            );");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"public class {_className} : Condition");
        sb.AppendLine("{");

        foreach (FieldEntry field in _fields)
            sb.AppendLine($"    private readonly {field.type} {field.name};");

        sb.AppendLine();
        sb.Append($"    public {_className}(");
        for (int i = 0; i < _fields.Count; i++)
            sb.Append($"{_fields[i].type} {ParamName(_fields[i].name)}{(i < _fields.Count - 1 ? ", " : "")}");
        sb.AppendLine(")");
        sb.AppendLine("    {");
        foreach (FieldEntry field in _fields)
            sb.AppendLine($"        this.{field.name} = {ParamName(field.name)};");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected override bool Statement()");
        sb.AppendLine("    {");
        sb.AppendLine("        // TODO: implement");
        sb.AppendLine("        return false;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
