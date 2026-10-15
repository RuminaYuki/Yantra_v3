#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Like VFX Graph's Blackboard: the table's exposed parameters, stored in an ExposedParametersSO inside
    // the table's file (one per file, shared by the sub-state machine tables embedded there).
    // A parameter is a name plus bindings (SO + [Exposed] field), so one name can set the same kind of
    // value in several places, like a VFX property wired to several nodes.
    // Grouped into categories (ExposedParameter._category, "" = Uncategorized).
    // + adds a parameter or a category, double-click the name renames it, right-click adds a binding, moves it to a category or
    // deletes it, right-click a category header renames it, drag a category header to reorder the
    // categories. A row's fold-out lists its bindings: each
    // edits its default value (the value in the SO) and can be changed or removed.
    public class ParameterBlackboard : Blackboard
    {
        private const string ParametersProperty = "_parameters";
        private const string NameProperty = "_name";
        private const string BindingsProperty = "_bindings";
        private const string TargetProperty = "_target";
        private const string FieldNameProperty = "_fieldName";
        private const string CategoryProperty = "_category";

        private const string UncategorizedTitle = "Uncategorized";

        // One section per category (like VFX Graph's Blackboard categories), rebuilt by Rebuild().
        private readonly List<BlackboardSection> _sections = new List<BlackboardSection>();
        private TransitionTableSO _table;

        public ParameterBlackboard(GraphView graphView) : base(graphView)
        {
            title = "Parameters";
            scrollable = true;
            SetPosition(new Rect(10f, 30f, 300f, 340f));

            addItemRequested = _ => ShowAddMenu();
            RegisterDropCallbacks();
            editTextRequested = (_, element, newName) => Rename((BlackboardField)element, newName);

            if (graphView is StateMachineGraphView stateMachineGraph)
            {
                stateMachineGraph.ParameterFieldsDeleted += DeleteFields;
            }

            // A condition put on a transition (or taken off) changes a row's color.
            RegisterCallback<AttachToPanelEvent>(_ => ObjectChangeEvents.changesPublished += OnChangesPublished);
            RegisterCallback<DetachFromPanelEvent>(_ => ObjectChangeEvents.changesPublished -= OnChangesPublished);
        }

        // Only when a color would change: a full rebuild while the Default value is being dragged would
        // throw that field away mid-edit.
        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            ExposedParametersSO parameters = Find(_table);
            if (parameters == null)
            {
                return;
            }

            _usedInFile = UsedInFile(_table);
            if (StatusesOf(parameters) != _shownStatuses)
            {
                Rebuild();
            }
            else
            {
                // A condition moved to another arrow keeps its color but moves its tag.
                (graphView as StateMachineGraphView)?.RefreshParameterTags();
            }
        }

        // What Rebuild last showed, to tell whether a change needs a rebuild.
        private string _shownStatuses = string.Empty;

        private string StatusesOf(ExposedParametersSO parameters)
        {
            return string.Join(";", parameters.Parameters.Select(parameter =>
                string.Join(",", parameter.Bindings.Select(BindingStatusOf))));
        }

        public void SetTable(TransitionTableSO table)
        {
            _table = table;

            // Tables made before the link existed get it when they're opened.
            ExposedParametersSO parameters = Find(_table);
            if (parameters != null)
            {
                LinkTables(parameters);
            }

            Rebuild();
        }

        public void Rebuild()
        {
            foreach (BlackboardSection section in _sections)
            {
                section.RemoveFromHierarchy();
            }

            _sections.Clear();
            subTitle = _table != null ? _table.name : string.Empty;

            // The badges on the graph follow the panel.
            (graphView as StateMachineGraphView)?.RefreshParameterTags();

            ExposedParametersSO parameters = Find(_table);
            if (parameters == null)
            {
                // Nothing exposed yet, but categories made with + still show, ready to drop into.
                foreach (string category in _emptyCategories)
                {
                    AddSection(CreateSection(category));
                }

                return;
            }

            _usedInFile = UsedInFile(_table);
            _shownStatuses = StatusesOf(parameters);

            // "New_TransitionTable · 1 missing · 2 unused"
            int missing = parameters.Parameters.Count(parameter => StatusOf(parameter) == RowStatus.Missing);
            int unused = parameters.Parameters.Count(parameter => StatusOf(parameter) == RowStatus.Unused);
            if (missing > 0)
            {
                subTitle += $" · {missing} missing";
            }

            if (unused > 0)
            {
                subTitle += $" · {unused} unused";
            }

            foreach (string category in ShownCategories(parameters))
            {
                int count = parameters.Parameters.Count(parameter => CategoryOf(parameter) == category);
                BlackboardSection section = CreateSection(category, count);

                for (int i = 0; i < parameters.Parameters.Count; i++)
                {
                    if (CategoryOf(parameters.Parameters[i]) == category)
                    {
                        section.Add(CreateRow(parameters.Parameters[i], i));
                    }
                }

                AddSection(section);
            }
        }

        private void AddSection(BlackboardSection section)
        {
            // An empty category says how to fill it.
            if (section.Q<BlackboardRow>() == null)
            {
                var hint = new Label("Empty: drag actions / conditions here, or use Move to Category.");
                hint.style.fontSize = 10;
                hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                hint.style.color = new Color(1f, 1f, 1f, 0.4f);
                hint.style.paddingLeft = 8f;
                hint.style.paddingBottom = 4f;
                hint.style.whiteSpace = WhiteSpace.Normal;
                section.Add(hint);
            }

            Add(section);
            _sections.Add(section);
        }

        // ---------- Categories ----------

        // Categories made with + that nothing is in yet. A category only exists through the parameters in
        // it, so an empty one lives here while the window is open; once something is moved in, it stays.
        private readonly List<string> _emptyCategories = new List<string>();

        // What the panel shows: the categories in use, then the empty ones made with +.
        private List<string> ShownCategories(ExposedParametersSO parameters)
        {
            List<string> categories = parameters != null ? CategoriesOf(parameters) : new List<string>();
            _emptyCategories.RemoveAll(categories.Contains);
            categories.AddRange(_emptyCategories);
            return categories;
        }

        private void AddEmptyCategory(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || ShownCategories(Find(_table)).Contains(name))
            {
                return;
            }

            _emptyCategories.Add(name);
            Rebuild();
        }

        // "" (no category) first, then the others in the order they first appear.
        // Also used by StateMachineParametersEditor so the Inspector groups the same way.
        public static List<string> CategoriesOf(ExposedParametersSO parameters)
        {
            var categories = new List<string>();
            if (parameters.Parameters.Any(parameter => CategoryOf(parameter) == string.Empty))
            {
                categories.Add(string.Empty);
            }

            foreach (ExposedParameter parameter in parameters.Parameters)
            {
                string category = CategoryOf(parameter);
                if (category != string.Empty && !categories.Contains(category))
                {
                    categories.Add(category);
                }
            }

            return categories;
        }

        public static string CategoryOf(ExposedParameter parameter)
        {
            return parameter.Category ?? string.Empty;
        }

        private static readonly Color SectionHeaderColor = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color SectionLineColor = new Color(1f, 1f, 1f, 0.15f);

        // "Attack (2)". The header is restyled so categories stand out from the rows under them.
        private BlackboardSection CreateSection(string category, int count = 0)
        {
            string name = category == string.Empty ? UncategorizedTitle : category;
            var section = new BlackboardSection
            {
                title = $"{name} ({count})",
                userData = category   // read by CategoryAt when something is dropped on it
            };

            StyleSectionHeader(section);

            if (category != string.Empty)
            {
                RegisterCategoryDrag(section, category);

                // Only for clicks on the header: a row's own menu bubbles up through the section.
                section.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    if (evt.target is VisualElement clicked && clicked.GetFirstAncestorOfType<BlackboardRow>() != null)
                    {
                        return;
                    }

                    Vector2 mouse = evt.mousePosition;
                    evt.menu.AppendAction("Add Parameter Here...", _ => OpenFieldSearch(
                        "Add Parameter",
                        null,
                        mouse,
                        (item, fieldName) => AddParameters(new List<(StateElementSO, string)> { (item, fieldName) }, category)));

                    evt.menu.AppendAction("Rename Category", _ => NamePromptWindow.Show(
                        "Rename Category",
                        category,
                        newName => RenameCategory(category, newName)));

                    if (_emptyCategories.Contains(category))
                    {
                        evt.menu.AppendAction("Remove Category", _ =>
                        {
                            _emptyCategories.Remove(category);
                            Rebuild();
                        });
                    }
                }));
            }

            return section;
        }

        // BlackboardSection's header ("sectionHeader" with its "sectionTitleLabel") is small and grey by
        // default; bigger bold text on a light band with a line under it.
        private static void StyleSectionHeader(BlackboardSection section)
        {
            VisualElement header = section.Q("sectionHeader");
            if (header != null)
            {
                header.style.backgroundColor = SectionHeaderColor;
                header.style.borderBottomWidth = 1f;
                header.style.borderBottomColor = SectionLineColor;
                header.style.paddingTop = 1f;
                header.style.paddingBottom = 1f;
                header.style.marginTop = 3f;
                header.style.marginBottom = 2f;
            }

            Label title = section.Q<Label>("sectionTitleLabel") ?? section.Q<Label>();
            if (title != null)
            {
                title.style.fontSize = 11;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = new Color(0.9f, 0.9f, 0.9f);
                title.style.paddingLeft = 4f;
            }
        }

        // Moves the parameter at `index` into the category ("" = none).
        private void SetCategory(int index, string category)
        {
            SetCategory(new List<int> { index }, category);
        }

        private void SetCategory(List<int> indexes, string category, bool recordUndo = true)
        {
            EditParameters(list =>
            {
                foreach (int index in indexes)
                {
                    if (index < list.arraySize)
                    {
                        list.GetArrayElementAtIndex(index)
                            .FindPropertyRelative(CategoryProperty)
                            .stringValue = category?.Trim() ?? string.Empty;
                    }
                }
            }, recordUndo: recordUndo);
        }

        private void RenameCategory(string oldName, string newName)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName) || newName == oldName)
            {
                return;
            }

            int empty = _emptyCategories.IndexOf(oldName);
            if (empty >= 0)
            {
                _emptyCategories[empty] = newName;
                Rebuild();
                return;
            }

            EditParameters(list =>
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty category = list.GetArrayElementAtIndex(i).FindPropertyRelative(CategoryProperty);
                    if (category.stringValue == oldName)
                    {
                        category.stringValue = newName;
                    }
                }
            });
        }

        private void AppendMoveToCategory(DropdownMenu menu, ExposedParametersSO parameters, ExposedParameter parameter, int index)
        {
            string current = CategoryOf(parameter);

            foreach (string category in ShownCategories(parameters))
            {
                string label = category == string.Empty ? UncategorizedTitle : category;
                menu.AppendAction(
                    $"Move to Category/{label}",
                    _ => SetCategory(index, category),
                    category == current ? DropdownMenuAction.Status.Checked | DropdownMenuAction.Status.Disabled
                                        : DropdownMenuAction.Status.Normal);
            }

            if (current != string.Empty && !CategoriesOf(parameters).Contains(string.Empty))
            {
                menu.AppendAction($"Move to Category/{UncategorizedTitle}", _ => SetCategory(index, string.Empty));
            }

            menu.AppendSeparator("Move to Category/");
            menu.AppendAction("Move to Category/New Category...", _ => NamePromptWindow.Show(
                "New Category",
                string.Empty,
                newName => SetCategory(index, newName)));
        }

        // ---------- The Blackboard asset ----------

        // The Blackboard asset in the table's file, or null if nothing was exposed yet.
        public static ExposedParametersSO Find(TransitionTableSO table)
        {
            if (table == null)
            {
                return null;
            }

            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(table))
                .OfType<ExposedParametersSO>()
                .FirstOrDefault();
        }

        // There's no AssetDatabase at runtime, so every table in the file (sub-state machine tables
        // included) keeps a reference to the Blackboard in TransitionTableSO._exposedParameters.
        private static void LinkTables(ExposedParametersSO parameters)
        {
            string path = AssetDatabase.GetAssetPath(parameters);
            bool isChanged = false;

            foreach (TransitionTableSO table in AssetDatabase.LoadAllAssetsAtPath(path).OfType<TransitionTableSO>())
            {
                var serializedTable = new SerializedObject(table);
                SerializedProperty link = serializedTable.FindProperty(StateMachineFields.ExposedParameters);

                if (link != null && link.objectReferenceValue != parameters)
                {
                    link.objectReferenceValue = parameters;
                    serializedTable.ApplyModifiedPropertiesWithoutUndo();
                    isChanged = true;
                }
            }

            if (isChanged)
            {
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
            }
        }

        // Every edit goes through here: the _parameters list, one Undo step (unless recordUndo is false),
        // saved, then the panel rebuilt.
        // A drop must not record Undo: Unity runs an Undo of its own when a drag ends, which would take
        // the edit straight back out (the same thing the Contents panel's folder drops ran into).
        private void EditParameters(Action<SerializedProperty> edit, bool rebuildNextFrame = false, bool recordUndo = true)
        {
            ExposedParametersSO parameters = Find(_table);
            if (parameters == null)
            {
                return;
            }

            var serialized = new SerializedObject(parameters);
            edit(serialized.FindProperty(ParametersProperty));

            if (recordUndo)
            {
                serialized.ApplyModifiedProperties();
            }
            else
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(parameters);
            }

            AssetDatabase.SaveAssetIfDirty(parameters);

            if (rebuildNextFrame)
            {
                schedule.Execute(Rebuild);
            }
            else
            {
                Rebuild();
            }
        }

        // ---------- Status ----------

        // Missing (red): no binding, or a binding's source / field is gone (or lost [Exposed]). Overrides
        // do nothing there and need fixing. Unused (yellow): the source is fine, but no state or transition
        // uses it, so overriding it changes nothing in play. A row shows its worst binding.
        private enum RowStatus { Ok, Unused, Missing }

        private static readonly Color MissingColor = new Color(0.95f, 0.35f, 0.35f);
        private static readonly Color UnusedColor = new Color(0.95f, 0.75f, 0.25f);

        // Actions and conditions used by any table in the file (the Blackboard is shared by them all).
        private HashSet<StateElementSO> _usedInFile = new HashSet<StateElementSO>();

        private static HashSet<StateElementSO> UsedInFile(TransitionTableSO table)
        {
            var used = new HashSet<StateElementSO>();
            foreach (TransitionTableSO fileTable in AssetDatabase
                         .LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(table))
                         .OfType<TransitionTableSO>())
            {
                used.UnionWith(TableContents.CollectUsed(fileTable).OfType<StateElementSO>());
            }

            return used;
        }

        private RowStatus BindingStatusOf(ExposedBinding binding)
        {
            if (FieldOf(binding) == null)
            {
                return RowStatus.Missing;
            }

            return _usedInFile.Contains(binding.Target) ? RowStatus.Ok : RowStatus.Unused;
        }

        private RowStatus StatusOf(ExposedParameter parameter)
        {
            return parameter.Bindings.Count == 0
                ? RowStatus.Missing
                : parameter.Bindings.Max(BindingStatusOf);
        }

        private static Color ColorOf(RowStatus status)
        {
            return status == RowStatus.Missing ? MissingColor : UnusedColor;
        }

        private static void ShowStatus(BlackboardRow row, BlackboardField field, RowStatus status, string message)
        {
            if (status == RowStatus.Ok)
            {
                return;
            }

            Color color = ColorOf(status);
            row.style.borderLeftWidth = 3f;
            row.style.borderLeftColor = color;
            field.tooltip = message;

            // BlackboardField's own labels: the name and the type text on the right.
            foreach (string labelName in new[] { "textLabel", "typeLabel" })
            {
                Label label = field.Q<Label>(labelName);
                if (label != null)
                {
                    label.style.color = color;
                }
            }
        }

        // ---------- Rows ----------

        private BlackboardRow CreateRow(ExposedParameter parameter, int index)
        {
            RowStatus status = StatusOf(parameter);
            SerializedProperty firstValue = parameter.Bindings
                .Select(ValuePropertyOf)
                .FirstOrDefault(value => value != null);

            // "Float", "Float ×3", or the status when something needs fixing.
            string typeText = status == RowStatus.Missing ? "Missing"
                : status == RowStatus.Unused ? "Unused"
                : firstValue != null ? firstValue.propertyType.ToString()
                : "Missing";
            if (parameter.Bindings.Count > 1)
            {
                typeText += $" ×{parameter.Bindings.Count}";
            }

            var field = new BlackboardField(null, parameter.Name, typeText)
            {
                userData = index
            };

            Type valueType = ValueTypeOf(parameter);
            field.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                Vector2 mouse = evt.mousePosition;
                evt.menu.AppendAction("Add Binding...", _ => OpenFieldSearch(
                    "Add Binding",
                    valueType,
                    mouse,
                    (item, fieldName) => AddBinding(index, item, fieldName)));

                ExposedParametersSO parameters = Find(_table);
                if (parameters != null)
                {
                    AppendMoveToCategory(evt.menu, parameters, parameter, index);
                }

                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete", _ => Delete(index));
            }));

            var details = new VisualElement();
            details.style.paddingLeft = 8f;
            details.style.paddingRight = 4f;
            details.style.paddingBottom = 4f;

            if (parameter.Bindings.Count == 0)
            {
                details.Add(new HelpBox("Not bound to anything. Add a binding below.", HelpBoxMessageType.Error));
            }

            for (int i = 0; i < parameter.Bindings.Count; i++)
            {
                details.Add(CreateBindingView(parameter.Bindings[i], index, i, valueType));
            }

            var addButton = new Button { text = "+ Add Binding", tooltip = "Also set this value on another action / condition." };
            addButton.style.alignSelf = Align.FlexStart;
            addButton.style.marginTop = 4f;
            addButton.clicked += () => OpenFieldSearch(
                "Add Binding",
                valueType,
                BelowOf(addButton),
                (item, fieldName) => AddBinding(index, item, fieldName));
            details.Add(addButton);

            var row = new BlackboardRow(field, details);
            ShowStatus(row, field, status, StatusMessage(parameter, status));
            return row;
        }

        private string StatusMessage(ExposedParameter parameter, RowStatus status)
        {
            switch (status)
            {
                case RowStatus.Missing:
                    return "A binding has no source, or its field is gone. Open the row to change or remove it.";
                case RowStatus.Unused:
                    return "A bound action / condition isn't used by any state or transition, so overriding it " +
                           "changes nothing there.";
                default:
                    return null;
            }
        }

        // One binding in a row's fold-out: where it points, its default value (the value in the SO),
        // Change (another SO / field of the same type) and × (remove).
        private VisualElement CreateBindingView(ExposedBinding binding, int parameterIndex, int bindingIndex, Type valueType)
        {
            RowStatus status = BindingStatusOf(binding);

            var view = new VisualElement();
            view.style.marginTop = 4f;
            view.style.paddingLeft = 4f;
            view.style.borderLeftWidth = 2f;
            view.style.borderLeftColor = status == RowStatus.Ok ? new Color(1f, 1f, 1f, 0.15f) : ColorOf(status);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            view.Add(header);

            string label = binding.Target != null
                ? $"{binding.Target.name} › {PathLabel(binding.FieldName)}"
                : "(deleted)";
            var title = new Label(label);
            title.style.flexGrow = 1f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            if (status != RowStatus.Ok)
            {
                title.style.color = ColorOf(status);
            }

            header.Add(title);

            var changeButton = new Button { text = "Change", tooltip = "Point this binding at another field." };
            changeButton.clicked += () => OpenFieldSearch(
                "Change Binding",
                valueType,
                BelowOf(changeButton),
                (item, fieldName) => ChangeBinding(parameterIndex, bindingIndex, item, fieldName));
            header.Add(changeButton);

            var removeButton = new Button(() => RemoveBinding(parameterIndex, bindingIndex))
            {
                text = "×",
                tooltip = "Remove this binding."
            };
            header.Add(removeButton);

            SerializedProperty value = ValuePropertyOf(binding);
            if (status == RowStatus.Missing || value == null)
            {
                view.Add(new HelpBox(
                    binding.Target == null
                        ? "The source was deleted. Change it, or remove this binding."
                        : $"{binding.FieldName} is missing or no longer [Exposed].",
                    HelpBoxMessageType.Error));
                return view;
            }

            if (status == RowStatus.Unused)
            {
                view.Add(new HelpBox(
                    $"No state or transition uses {binding.Target.name}, so overriding it changes nothing.",
                    HelpBoxMessageType.Warning));
            }

            var source = new ObjectField("Source") { objectType = typeof(ScriptableObject), value = binding.Target };
            source.SetEnabled(false);
            view.Add(source);

            var defaultField = new PropertyField(value, "Default");
            defaultField.Bind(value.serializedObject);
            view.Add(defaultField);

            return view;
        }

        // ---------- Bindings: lookups ----------

        // The C# type of the value a binding sets (float / int), or null if its path is gone.
        private static Type FieldOf(ExposedBinding binding)
        {
            return binding.Target != null ? FindExposedPath(binding.Target.GetType(), binding.FieldName) : null;
        }

        // FindProperty takes a dotted path too ("_attackParameters.damageAmount").
        private static SerializedProperty ValuePropertyOf(ExposedBinding binding)
        {
            return FieldOf(binding) != null
                ? new SerializedObject(binding.Target).FindProperty(binding.FieldName)
                : null;
        }

        // The C# type of the parameter's value (from its first working binding), or null if none works.
        // Every binding of one parameter has to be this type, since they all get the same value.
        private static Type ValueTypeOf(ExposedParameter parameter)
        {
            return parameter.Bindings.Select(FieldOf).FirstOrDefault(type => type != null);
        }

        // "_attackParameters.damageAmount" -> "Attack Parameters › Damage Amount".
        private static string PathLabel(string path)
        {
            return string.Join(" › ", (path ?? string.Empty).Split('.').Select(ObjectNames.NicifyVariableName));
        }

        // Is this SO / field already bound by any parameter? A field set by two parameters would fight.
        private static bool IsBound(ExposedParametersSO parameters, StateElementSO item, string fieldName)
        {
            return parameters != null &&
                   parameters.Parameters.Any(parameter =>
                       parameter.Bindings.Any(binding => binding.Target == item && binding.FieldName == fieldName));
        }

        // ---------- Field choices ----------

        // One pickable field. Taken = already bound by a parameter (shown with ✓, not pickable).
        private struct FieldChoice
        {
            public StateElementSO Item;
            public string FieldName;
            public bool IsTaken;
            public string Table;   // see Candidates
        }

        // Every [Exposed] field of the file's actions and conditions, only of `valueType` when given.
        private IEnumerable<FieldChoice> FieldChoices(Type valueType = null)
        {
            if (_table == null)
            {
                yield break;
            }

            ExposedParametersSO parameters = Find(_table);

            foreach ((StateElementSO item, string table) in Candidates())
            {
                foreach ((string path, Type type) in ExposedPathsOf(item.GetType()))
                {
                    if (valueType != null && type != valueType)
                    {
                        continue;
                    }

                    yield return new FieldChoice
                    {
                        Item = item,
                        FieldName = path,
                        IsTaken = IsBound(parameters, item, path),
                        Table = table
                    };
                }
            }
        }

        // ---------- Field search popup ----------

        private FieldSearchProvider _searchProvider;

        // A searchable popup of the table's [Exposed] fields (only of `valueType` when given), opened at a
        // point in this panel's window. Works from buttons, context menus and the + button alike.
        private void OpenFieldSearch(string popupTitle, Type valueType, Vector2 worldPosition, Action<StateElementSO, string> onPicked)
        {
            if (_searchProvider == null)
            {
                _searchProvider = ScriptableObject.CreateInstance<FieldSearchProvider>();
                _searchProvider.hideFlags = HideFlags.HideAndDontSave;
                RegisterCallback<DetachFromPanelEvent>(_ => DestroySearchProvider());
            }

            List<FieldSearchProvider.Choice> choices = FieldChoices(valueType)
                .Select(choice => new FieldSearchProvider.Choice
                {
                    Item = choice.Item,
                    FieldName = choice.FieldName,
                    Label = PathLabel(choice.FieldName),
                    IsTaken = choice.IsTaken,
                    Table = choice.Table
                })
                .ToList();

            _searchProvider.Setup(popupTitle, choices, onPicked);

            // SearchWindow wants screen coordinates: the window's corner plus the point inside it.
            EditorWindow window = (graphView as StateMachineGraphView)?.Window ?? EditorWindow.focusedWindow;
            Vector2 screenPosition = (window != null ? window.position.position : Vector2.zero) + worldPosition;
            SearchWindow.Open(new SearchWindowContext(screenPosition), _searchProvider);
        }

        private void DestroySearchProvider()
        {
            if (_searchProvider != null)
            {
                UnityEngine.Object.DestroyImmediate(_searchProvider);
                _searchProvider = null;
            }
        }

        // Just under an element, in window space.
        private static Vector2 BelowOf(VisualElement element)
        {
            return new Vector2(element.worldBound.xMin, element.worldBound.yMax);
        }

        // The + button (a real click, so an IMGUI menu opens here): a parameter, or a category, like VFX Graph.
        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Parameter..."), false, () => OpenFieldSearch(
                "Add Parameter",
                null,
                new Vector2(worldBound.xMax - 20f, worldBound.yMin + 30f),
                (item, fieldName) => AddParameters(new List<(StateElementSO, string)> { (item, fieldName) }, string.Empty)));
            menu.AddItem(new GUIContent("Category..."), false, () => NamePromptWindow.Show(
                "New Category",
                string.Empty,
                AddEmptyCategory));
            menu.ShowAsContext();
        }

        // Actions and conditions any table in the file uses (shared ones too, since the Blackboard is the
        // whole file's), plus ones in the file nothing uses. Each once, with where the popup lists it:
        // null for the open table, else the first other table that uses it ("Attack" for
        // "Attack_TransitionTable"), or FieldSearchProvider.UnusedTable.
        private List<(StateElementSO Item, string Table)> Candidates()
        {
            var candidates = new List<(StateElementSO Item, string Table)>();
            if (_table == null)
            {
                return candidates;
            }

            var seen = new HashSet<StateElementSO>();
            void AddFrom(IEnumerable<ScriptableObject> items, string table)
            {
                foreach (StateElementSO item in items.OfType<StateElementSO>())
                {
                    if (seen.Add(item))
                    {
                        candidates.Add((item, table));
                    }
                }
            }

            AddFrom(TableContents.CollectUsed(_table), null);

            foreach (TransitionTableSO other in RelatedTables().Where(table => table != _table).OrderBy(table => table.name))
            {
                AddFrom(TableContents.CollectUsed(other), EmbeddedAssets.TablePrefix(other.name));
            }

            AddFrom(TableContents.CollectEmbedded(_table), FieldSearchProvider.UnusedTable);

            return candidates.OrderBy(candidate => candidate.Item.name).ToList();
        }

        // The tables in this table's file, and the sub-state machine tables they run (down through
        // nested ones), in whatever file those are. At runtime a character resolves bindings through
        // every Blackboard it registered, so a binding here reaches a child table's items too.
        private List<TransitionTableSO> RelatedTables()
        {
            var tables = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_table))
                .OfType<TransitionTableSO>()
                .ToList();

            for (int i = 0; i < tables.Count; i++)
            {
                foreach (StateSO state in TableContents.CollectUsed(tables[i]).OfType<StateSO>())
                {
                    foreach (TransitionTableSO subTable in StateNodeView.FindSubTables(state))
                    {
                        if (subTable != null && !tables.Contains(subTable))
                        {
                            tables.Add(subTable);
                        }
                    }
                }
            }

            return tables;
        }

        // ---------- Edits ----------

        // New parameters, one binding each. One Undo step for the lot (a drop can add several).
        private void AddParameters(List<(StateElementSO Target, string FieldName)> fields, string category)
        {
            if (fields.Count == 0)
            {
                return;
            }

            Undo.SetCurrentGroupName("Expose Parameter");
            int group = Undo.GetCurrentGroup();

            ExposedParametersSO parameters = Find(_table);
            if (parameters == null)
            {
                parameters = EmbeddedAssets.Create<ExposedParametersSO>(_table, "Exposed_Parameters");
                LinkTables(parameters);
            }

            List<string> names = parameters.Parameters.Select(parameter => parameter.Name).ToList();

            EditParameters(list =>
            {
                foreach ((StateElementSO target, string fieldName) in fields)
                {
                    // "Chase_Condition.Distance", "Attack_Condition.ExtraSeconds"
                    // Inside a struct, the last part: "Attack_Action.DamageAmount".
                    string fieldPart = fieldName.Substring(fieldName.LastIndexOf('.') + 1);
                    string baseName = $"{target.name}.{ObjectNames.NicifyVariableName(fieldPart).Replace(" ", string.Empty)}";
                    string name = ObjectNames.GetUniqueName(names.ToArray(), baseName);
                    names.Add(name);

                    list.arraySize++;
                    SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                    entry.FindPropertyRelative(NameProperty).stringValue = name;
                    entry.FindPropertyRelative(CategoryProperty).stringValue = category;

                    SerializedProperty bindings = entry.FindPropertyRelative(BindingsProperty);
                    bindings.arraySize = 1;
                    SetBinding(bindings.GetArrayElementAtIndex(0), target, fieldName);
                }
            });

            Undo.CollapseUndoOperations(group);
        }

        private void AddBindings(int parameterIndex, List<(StateElementSO Target, string FieldName)> fields)
        {
            if (fields.Count == 0)
            {
                return;
            }

            EditParameters(list =>
            {
                SerializedProperty bindings = list.GetArrayElementAtIndex(parameterIndex).FindPropertyRelative(BindingsProperty);
                foreach ((StateElementSO target, string fieldName) in fields)
                {
                    bindings.arraySize++;
                    SetBinding(bindings.GetArrayElementAtIndex(bindings.arraySize - 1), target, fieldName);
                }
            });
        }

        private void AddBinding(int parameterIndex, StateElementSO target, string fieldName)
        {
            AddBindings(parameterIndex, new List<(StateElementSO, string)> { (target, fieldName) });
        }

        // Same name, another SO / field: characters that override this name now set the new field.
        private void ChangeBinding(int parameterIndex, int bindingIndex, StateElementSO target, string fieldName)
        {
            EditParameters(list => SetBinding(
                list.GetArrayElementAtIndex(parameterIndex)
                    .FindPropertyRelative(BindingsProperty)
                    .GetArrayElementAtIndex(bindingIndex),
                target,
                fieldName));
        }

        private void RemoveBinding(int parameterIndex, int bindingIndex)
        {
            EditParameters(list => list
                .GetArrayElementAtIndex(parameterIndex)
                .FindPropertyRelative(BindingsProperty)
                .DeleteArrayElementAtIndex(bindingIndex));
        }

        private static void SetBinding(SerializedProperty binding, StateElementSO target, string fieldName)
        {
            binding.FindPropertyRelative(TargetProperty).objectReferenceValue = target;
            binding.FindPropertyRelative(FieldNameProperty).stringValue = fieldName;
        }

        // ---------- Drop ----------

        // Actions / conditions dragged from Contents (or the Project window, if this table uses them).
        // On a row: a menu adds bindings to that parameter (same type only), like wiring a VFX property
        // to another node. Elsewhere: a menu picks fields to become new parameters, in the category
        // dropped on.
        private void RegisterDropCallbacks()
        {
            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                // A category header dragged to a new place (see RegisterCategoryDrag).
                if (DraggedCategory() != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    (BlackboardSection section, bool isAfter) = InsertTargetAt(evt.target, evt.mousePosition);
                    MarkInsert(section, isAfter);
                    evt.StopPropagation();
                    return;
                }

                // Rows dragged to another category (BlackboardField's own drag, see DraggedRows).
                if (DraggedRows().Count > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    HighlightDropSection(SectionAt(evt.target));
                    evt.StopPropagation();
                    return;
                }

                if (DroppedItems().Count == 0)
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                HighlightDropSection(SectionAt(evt.target));
                evt.StopPropagation();
            });

            RegisterCallback<DragLeaveEvent>(_ =>
            {
                HighlightDropSection(null);
                MarkInsert(null, false);
            });
            RegisterCallback<DragExitedEvent>(_ =>
            {
                HighlightDropSection(null);
                MarkInsert(null, false);
            });

            RegisterCallback<DragPerformEvent>(evt =>
            {
                HighlightDropSection(null);

                string draggedCategory = DraggedCategory();
                if (draggedCategory != null)
                {
                    (BlackboardSection section, bool isAfter) = InsertTargetAt(evt.target, evt.mousePosition);
                    MarkInsert(null, false);
                    DragAndDrop.AcceptDrag();
                    evt.StopPropagation();

                    if (section != null)
                    {
                        string targetCategory = section.userData as string ?? string.Empty;
                        schedule.Execute(() => MoveCategory(draggedCategory, targetCategory, isAfter));
                    }

                    return;
                }

                List<int> draggedRows = DraggedRows();
                if (draggedRows.Count > 0)
                {
                    DragAndDrop.AcceptDrag();
                    evt.StopPropagation();

                    // Dropped outside every category = Uncategorized.
                    string target = CategoryAt(evt.target);
                    schedule.Execute(() => SetCategory(draggedRows, target, recordUndo: false));
                    return;
                }

                List<StateElementSO> items = DroppedItems();
                if (items.Count == 0)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                evt.StopPropagation();

                // The drag data is gone once the drop ends, so the menu gets its own copies.
                int rowIndex = RowIndexAt(evt.target);
                string category = CategoryAt(evt.target);
                Vector2 mouse = evt.mousePosition;
                schedule.Execute(() => ShowDropMenu(items, rowIndex, category, mouse));
            });
        }

        // Add All, or one field. Already bound ones are ticked and greyed out.
        // A UI Toolkit menu, since an IMGUI one can't open from a drop.
        private void ShowDropMenu(List<StateElementSO> items, int rowIndex, string category, Vector2 mouse)
        {
            ExposedParametersSO parameters = Find(_table);
            bool isOnRow = parameters != null && rowIndex >= 0 && rowIndex < parameters.Parameters.Count;
            Type valueType = isOnRow ? ValueTypeOf(parameters.Parameters[rowIndex]) : null;

            var menu = new GenericDropdownMenu();
            var free = new List<(StateElementSO, string)>();

            foreach (StateElementSO item in items)
            {
                foreach ((string path, Type type) in ExposedPathsOf(item.GetType()))
                {
                    if (valueType != null && type != valueType)
                    {
                        continue;
                    }

                    // One item: "Min Duration". Several: "TestCDTimer_Condition › Min Duration".
                    string label = PathLabel(path);
                    if (items.Count > 1)
                    {
                        label = $"{item.name} › {label}";
                    }

                    if (IsBound(parameters, item, path))
                    {
                        menu.AddDisabledItem(label, true);
                        continue;
                    }

                    string fieldName = path;
                    free.Add((item, fieldName));
                    menu.AddItem(label, false, () =>
                    {
                        var picked = new List<(StateElementSO, string)> { (item, fieldName) };
                        if (isOnRow)
                        {
                            AddBindings(rowIndex, picked);
                        }
                        else
                        {
                            AddParameters(picked, category);
                        }
                    });
                }
            }

            if (free.Count > 1)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem($"Add All ({free.Count})", false, () =>
                {
                    if (isOnRow)
                    {
                        AddBindings(rowIndex, free);
                    }
                    else
                    {
                        AddParameters(free, category);
                    }
                });
            }

            if (free.Count == 0 && isOnRow)
            {
                menu.AddDisabledItem($"No free fields of type {valueType?.Name ?? "?"} to bind", false);
            }

            menu.DropDown(new Rect(mouse, Vector2.zero), this, DropdownMenuSizeMode.Auto);
        }

        // Dragged actions / conditions this table uses that have at least one [Exposed] field.
        private List<StateElementSO> DroppedItems()
        {
            if (_table == null)
            {
                return new List<StateElementSO>();
            }

            var candidates = new HashSet<StateElementSO>(Candidates().Select(candidate => candidate.Item));
            return DragAndDrop.objectReferences
                .OfType<StateElementSO>()
                .Distinct()
                .Where(item => candidates.Contains(item) && ExposedPathsOf(item.GetType()).Any())
                .ToList();
        }

        // The category of the section under the mouse ("" outside any, or on Uncategorized).
        private static string CategoryAt(IEventHandler target)
        {
            return SectionAt(target)?.userData as string ?? string.Empty;
        }

        private static BlackboardSection SectionAt(IEventHandler target)
        {
            var element = target as VisualElement;
            return element as BlackboardSection ?? element?.GetFirstAncestorOfType<BlackboardSection>();
        }

        // ---------- Dragging rows between categories ----------

        // Like dragging into a folder in Contents. BlackboardField already starts a drag of the selected
        // rows when its name is dragged (GraphView's "DragSelection"); starting a second one here would
        // make Unity complain about two drags at once. So this only reads that one.
        private const string DragSelectionKey = "DragSelection";

        private BlackboardSection _highlightedSection;

        private static List<int> DraggedRows()
        {
            var rows = new List<int>();
            if (DragAndDrop.GetGenericData(DragSelectionKey) is List<ISelectable> selection)
            {
                foreach (ISelectable selected in selection)
                {
                    if (selected is BlackboardField field && field.userData is int index)
                    {
                        rows.Add(index);
                    }
                }
            }

            return rows;
        }

        // The category under the mouse lights up while something is dragged over it.
        private void HighlightDropSection(BlackboardSection section)
        {
            if (section == _highlightedSection)
            {
                return;
            }

            if (_highlightedSection != null)
            {
                _highlightedSection.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            }

            _highlightedSection = section;

            if (_highlightedSection != null)
            {
                _highlightedSection.style.backgroundColor = new Color(0.27f, 0.6f, 1f, 0.15f);
            }
        }

        // ---------- Dragging categories ----------

        // Like VFX Graph: drag a category's header above or below another one. The order is the order of
        // the _parameters list (see CategoriesOf), so a category's parameters move there as one block.
        // Uncategorized stays on top. Empty categories (only in this window) keep their order among
        // themselves, after the others, until something is put in them.
        private const string CategoryDragKey = "ParameterCategoryDrag";
        private const float CategoryDragStartDistance = 5f;
        private static readonly Color InsertLineColor = new Color(0.27f, 0.6f, 1f, 0.9f);

        // Press on a header, then move a few pixels: starts the drag.
        private string _categoryDragCandidate;
        private Vector2 _categoryDragStart;

        // The section showing the insert line.
        private BlackboardSection _insertMarked;

        // On the section, in the trickle-down phase, by where the mouse is: the header and its label may
        // not take clicks themselves (the section gets them), and the Blackboard's own Dragger (which
        // moves the whole panel) would take the mouse on MouseDown. So the section stops a press on its
        // header before anything else sees it and holds the mouse until the drag starts.
        private void RegisterCategoryDrag(BlackboardSection section, string category)
        {
            VisualElement header = section.Q("sectionHeader");
            if (header == null)
            {
                return;
            }

            section.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || !header.worldBound.Contains(evt.mousePosition))
                {
                    return;
                }

                _categoryDragCandidate = category;
                _categoryDragStart = evt.mousePosition;
                section.CaptureMouse();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);

            section.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (_categoryDragCandidate != category ||
                    (evt.pressedButtons & 1) == 0 ||
                    Vector2.Distance(evt.mousePosition, _categoryDragStart) < CategoryDragStartDistance)
                {
                    return;
                }

                _categoryDragCandidate = null;
                section.ReleaseMouse();

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(CategoryDragKey, category);
                DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
                DragAndDrop.StartDrag(category);
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);

            section.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0 || _categoryDragCandidate != category)
                {
                    return;
                }

                _categoryDragCandidate = null;
                if (section.HasMouseCapture())
                {
                    section.ReleaseMouse();
                }
            }, TrickleDown.TrickleDown);
        }

        private static string DraggedCategory()
        {
            return DragAndDrop.GetGenericData(CategoryDragKey) as string;
        }

        // The section the dragged category goes next to: before it (mouse on its upper half) or after it.
        // Below every section = after the last one. Nothing goes above Uncategorized.
        private (BlackboardSection Section, bool IsAfter) InsertTargetAt(IEventHandler target, Vector2 mouse)
        {
            BlackboardSection section = SectionAt(target);
            if (section == null)
            {
                return (_sections.LastOrDefault(), true);
            }

            bool isAfter = mouse.y > section.worldBound.center.y ||
                           (section.userData as string ?? string.Empty) == string.Empty;
            return (section, isAfter);
        }

        // A blue line over or under the section where the category would go.
        private void MarkInsert(BlackboardSection section, bool isAfter)
        {
            if (_insertMarked != null)
            {
                _insertMarked.style.borderTopWidth = new StyleFloat(StyleKeyword.Null);
                _insertMarked.style.borderBottomWidth = new StyleFloat(StyleKeyword.Null);
            }

            _insertMarked = section;
            if (section == null)
            {
                return;
            }

            if (isAfter)
            {
                section.style.borderBottomWidth = 2f;
                section.style.borderBottomColor = InsertLineColor;
            }
            else
            {
                section.style.borderTopWidth = 2f;
                section.style.borderTopColor = InsertLineColor;
            }
        }

        // Without Undo, like the other drops (see EditParameters).
        private void MoveCategory(string category, string targetCategory, bool isAfter)
        {
            if (category == targetCategory)
            {
                return;
            }

            ExposedParametersSO parameters = Find(_table);
            List<string> order = ShownCategories(parameters);
            order.Remove(category);

            int index = order.IndexOf(targetCategory);
            index = index < 0 ? order.Count : isAfter ? index + 1 : index;

            int first = order.Count > 0 && order[0] == string.Empty ? 1 : 0;
            order.Insert(Mathf.Clamp(index, first, order.Count), category);

            List<string> empties = order.Where(_emptyCategories.Contains).ToList();
            _emptyCategories.Clear();
            _emptyCategories.AddRange(empties);

            if (parameters == null || empties.Contains(category))
            {
                Rebuild();
                return;
            }

            var rank = new Dictionary<string, int>();
            for (int i = 0; i < order.Count; i++)
            {
                rank[order[i]] = i;
            }

            EditParameters(list => SortByCategory(list, rank), recordUndo: false);
        }

        // Puts the parameters in category order, keeping their order inside each category.
        private static void SortByCategory(SerializedProperty list, Dictionary<string, int> rank)
        {
            var categories = new List<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                categories.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative(CategoryProperty).stringValue ?? string.Empty);
            }

            List<int> wanted = Enumerable.Range(0, categories.Count)
                .OrderBy(i => rank.TryGetValue(categories[i], out int r) ? r : int.MaxValue)
                .ToList();

            // current[position] = the element's original index.
            List<int> current = Enumerable.Range(0, categories.Count).ToList();
            for (int target = 0; target < wanted.Count; target++)
            {
                int from = current.IndexOf(wanted[target]);
                if (from == target)
                {
                    continue;
                }

                list.MoveArrayElement(from, target);
                int moved = current[from];
                current.RemoveAt(from);
                current.Insert(target, moved);
            }
        }

        // The parameter index of the row under the mouse, or -1.
        private static int RowIndexAt(IEventHandler target)
        {
            var element = target as VisualElement;
            BlackboardRow row = element as BlackboardRow ?? element?.GetFirstAncestorOfType<BlackboardRow>();
            return row?.Q<BlackboardField>()?.userData is int index ? index : -1;
        }

        // ---------- Rename / Delete ----------

        private void Rename(BlackboardField field, string newName)
        {
            ExposedParametersSO parameters = Find(_table);
            int index = (int)field.userData;
            newName = newName?.Trim();

            if (parameters == null ||
                string.IsNullOrEmpty(newName) ||
                newName == field.text)
            {
                return;
            }

            if (parameters.Parameters.Any(p => p.Name == newName))
            {
                Debug.LogWarning($"A parameter named \"{newName}\" already exists.");
                return;
            }

            EditParameters(list => list
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative(NameProperty)
                .stringValue = newName);
        }

        private void Delete(int index)
        {
            DeleteAt(new[] { index });
        }

        // Delete key in the graph with rows selected (several at once too).
        private void DeleteFields(List<BlackboardField> fields)
        {
            DeleteAt(fields.Where(field => field.userData is int).Select(field => (int)field.userData));
        }

        private void DeleteAt(IEnumerable<int> indexes)
        {
            // The graph may still be in the middle of its delete, so the panel rebuilds next frame.
            EditParameters(list =>
            {
                // Last first, so the earlier indexes stay valid.
                foreach (int index in indexes.Distinct().OrderByDescending(index => index))
                {
                    if (index < list.arraySize)
                    {
                        list.DeleteArrayElementAtIndex(index);
                    }
                }
            }, rebuildNextFrame: true);
        }

        // ---------- [Exposed] fields ----------

        // Every [Exposed] field of the type and its base classes (private ones included).
        private static IEnumerable<FieldInfo> ExposedFieldsOf(Type type)
        {
            for (Type current = type; current != null && current != typeof(ScriptableObject); current = current.BaseType)
            {
                FieldInfo[] fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (FieldInfo field in fields)
                {
                    if (field.IsDefined(typeof(ExposedAttribute), false))
                    {
                        yield return field;
                    }
                }
            }
        }

        // How deep into nested structs the panel looks ("_a.b.c").
        private const int MaxStructDepth = 3;

        // Every path a parameter can set: an [Exposed] field of a settable type (see IsSettable), or the settable
        // fields inside an [Exposed] struct ("_attackParameters.damageAmount"). Those inside don't need
        // their own [Exposed]: exposing the struct offers all of them.
        private static IEnumerable<(string Path, Type Type)> ExposedPathsOf(Type type)
        {
            foreach (FieldInfo field in ExposedFieldsOf(type))
            {
                foreach ((string Path, Type Type) leaf in LeavesOf(field.Name, field.FieldType, 0))
                {
                    yield return leaf;
                }
            }
        }

        private static IEnumerable<(string Path, Type Type)> LeavesOf(string path, Type type, int depth)
        {
            if (IsSettable(type))
            {
                yield return (path, type);
                yield break;
            }

            if (depth >= MaxStructDepth || !IsInlineStruct(type))
            {
                yield break;
            }

            foreach (FieldInfo inner in SavedFieldsOf(type))
            {
                foreach ((string Path, Type Type) leaf in LeavesOf($"{path}.{inner.Name}", inner.FieldType, depth + 1))
                {
                    yield return leaf;
                }
            }
        }

        // What StateMachineParameters.ConvertValue can set: float, int, enum, bool, string, Vector3, Vector2,
        // Color, and asset references (SO, AnimationClip, InputActionReference, ...).
        private static bool IsSettable(Type type)
        {
            return type == typeof(float) ||
                   type == typeof(int) ||
                   type.IsEnum ||
                   type == typeof(bool) ||
                   type == typeof(string) ||
                   type == typeof(Vector3) ||
                   type == typeof(Vector2) ||
                   type == typeof(Color) ||
                   typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        // A [Serializable] struct / class Unity saves inline: not an asset reference, not a list.
        private static bool IsInlineStruct(Type type)
        {
            return !type.IsPrimitive &&
                   !type.IsEnum &&
                   type != typeof(string) &&
                   !typeof(UnityEngine.Object).IsAssignableFrom(type) &&
                   !typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
                   type.IsDefined(typeof(SerializableAttribute), false);
        }

        // Fields Unity saves inside a struct / class: public ones, and private ones with [SerializeField].
        private static IEnumerable<FieldInfo> SavedFieldsOf(Type type)
        {
            for (Type current = type; current != null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
            {
                FieldInfo[] fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (FieldInfo field in fields)
                {
                    if (!field.IsNotSerialized && (field.IsPublic || field.IsDefined(typeof(SerializeField), false)))
                    {
                        yield return field;
                    }
                }
            }
        }

        // The value type at `path`, or null when it was renamed, deleted, or lost its [Exposed].
        private static Type FindExposedPath(Type type, string path)
        {
            return ExposedPathsOf(type)
                .Where(leaf => leaf.Path == path)
                .Select(leaf => leaf.Type)
                .FirstOrDefault();
        }
    }
}

#endif
