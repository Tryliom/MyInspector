using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MyInspector.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(UnityEngine.Object), true)]
    public class MyInspector : UnityEditor.Editor
    {
        private const int LayerCount = 4;

        private class Item
        {
            public SerializedProperty Property;
            public MethodInfo Method;
            public FieldInfo Field;
            public PropertyInfo Native;
            public int Order;
            public float Space;
            public string Title;
            public ListDrawerSettingsAttribute List;
            public TableListAttribute Table;
            public TabAttribute Tab;
            public FoldoutAttribute Foldout;
            public BoxGroupAttribute Box;
            public HorizontalGroupAttribute Horizontal;
            public VerticalGroupAttribute Vertical;

            private string _layer1Key;
            private bool _layer1Computed;

            public string Key(int layer)
            {
                switch (layer)
                {
                    case 0: return Tab?.Group;
                    case 1:
                        if (!_layer1Computed)
                        {
                            _layer1Key = Foldout != null ? "f:" + Foldout.Name : Box != null ? "b:" + Box.Name : null;
                            _layer1Computed = true;
                        }

                        return _layer1Key;
                    case 2: return Horizontal?.Name;
                    case 3: return Vertical?.Name;
                    default: return null;
                }
            }

            public MemberInfo Member => Method ?? (MemberInfo)Field ?? Native;

            public bool HasGroup => Tab != null || Foldout != null || Box != null || Horizontal != null || Vertical != null;
        }

        private List<SerializedProperty> _serializedProperties = new List<SerializedProperty>();
        private List<FieldInfo> _nonSerializedFields;
        private List<PropertyInfo> _nativeProperties;
        private List<MethodInfo> _methods;
        private Dictionary<string, SavedBool> _foldouts = new Dictionary<string, SavedBool>();
        private string _foldoutPrefix = string.Empty;
        private List<Item> _items = new List<Item>();
        private Dictionary<string, UnityEditorInternal.ReorderableList> _lists = new Dictionary<string, UnityEditorInternal.ReorderableList>();
        private Dictionary<string, SavedBool> _listFoldouts = new Dictionary<string, SavedBool>();

        protected virtual void OnEnable()
        {
            _nonSerializedFields = ReflectionUtility.GetAllFields(
                target, f => f.GetCustomAttributes(typeof(ShowNonSerializedFieldAttribute), true).Length > 0).ToList();

            _nativeProperties = ReflectionUtility.GetAllProperties(
                target, p => p.GetCustomAttributes(typeof(ShowNativePropertyAttribute), true).Length > 0).ToList();

            _methods = ReflectionUtility.GetAllMethods(
                target, m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0).ToList();

            InvokeInspectorInit();
        }

        private void InvokeInspectorInit()
        {
            foreach (var field in _nonSerializedFields)
            {
                var init = field.GetCustomAttribute<OnInspectorInitAttribute>(true);

                if (init == null)
                {
                    continue;
                }

                MethodInfo callback = null;

                // Private methods of a base class are not returned when searching from a derived type
                for (var type = target.GetType(); type != null && callback == null; type = type.BaseType)
                {
                    callback = type.GetMethod(
                        init.Callback,
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        null,
                        Type.EmptyTypes,
                        null);
                }

                callback?.Invoke(target, null);
            }
        }

        private static readonly Dictionary<DisplayAsStringAttribute, GUIStyle> DisplayAsStringStyles = new Dictionary<DisplayAsStringAttribute, GUIStyle>();

        private static GUIStyle GetDisplayAsStringStyle(DisplayAsStringAttribute attribute)
        {
            if (DisplayAsStringStyles.TryGetValue(attribute, out var cached) && cached != null)
            {
                return cached;
            }

            var style = new GUIStyle(EditorStyles.label)
            {
                wordWrap = !attribute.Overflow,
                richText = attribute.EnableRichText,
                fontSize = attribute.FontSize > 0 ? attribute.FontSize : EditorStyles.label.fontSize,
                alignment = attribute.Alignment == TextAlignment.Center ? TextAnchor.MiddleCenter
                    : attribute.Alignment == TextAlignment.Right ? TextAnchor.MiddleRight
                    : TextAnchor.MiddleLeft
            };

            DisplayAsStringStyles[attribute] = style;

            return style;
        }

        private void DrawDisplayAsString(FieldInfo field, DisplayAsStringAttribute attribute, string label, object value)
        {
            var text = value?.ToString() ?? string.Empty;
            var style = GetDisplayAsStringStyle(attribute);
            var content = new GUIContent(text);

            if (string.IsNullOrEmpty(label))
            {
                var width = EditorGUIUtility.currentViewWidth - InspectorStyle.DisplayAsStringHorizontalMargin;
                var height = Mathf.Max(EditorGUIUtility.singleLineHeight, style.CalcHeight(content, width));
                var rect = EditorGUILayout.GetControlRect(false, height);

                EditorGUI.LabelField(rect, content, style);
            }
            else
            {
                EditorGUILayout.LabelField(label, text, style);
            }
        }

        protected virtual void OnDisable()
        {
            ReorderableListPropertyDrawer.Instance.ClearCache();
        }

        public override void OnInspectorGUI()
        {
            var previousLabelWidth = EditorGUIUtility.labelWidth;

            // Unity defaults to ~45% of the view width, labels only need 30%
            EditorGUIUtility.labelWidth = Mathf.Max(80f, EditorGUIUtility.currentViewWidth * 0.3f);

            try
            {
                DrawInspector();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        private void DrawInspector()
        {
            MyListDrawer.BeginInspector(target);
            GetSerializedProperties(ref _serializedProperties);

            var anyMyAttribute = _nonSerializedFields.Count > 0 || _nativeProperties.Count > 0 || _methods.Count > 0;

            for (var i = 0; i < _serializedProperties.Count && !anyMyAttribute; i++)
            {
                var p = _serializedProperties[i];

                anyMyAttribute = PropertyUtility.GetAttribute<IMyAttribute>(p) != null || IsSerializeReference(p)
                    || MyListDrawer.IsList(p) || p.propertyType == SerializedPropertyType.Generic;
            }

            if (!anyMyAttribute)
            {
                DrawDefaultInspector();
            }
            else
            {
                DrawSerializedProperties();
            }
        }

        private static FieldInfo GetField(SerializedProperty property)
        {
            return ReflectionUtility.GetField(PropertyUtility.GetTargetObjectWithProperty(property), property.name);
        }

        private static bool IsSerializeReference(SerializedProperty property)
        {
            var field = GetField(property);

            return field != null && AttributeCache.Get<SerializeReference>(field) != null;
        }

        private static Type GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType)
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        private static int Depth(Type type)
        {
            var depth = 0;

            while (type != null)
            {
                depth++;
                type = type.BaseType;
            }

            return depth;
        }

        protected void GetSerializedProperties(ref List<SerializedProperty> outSerializedProperties)
        {
            outSerializedProperties.Clear();
            using (var iterator = serializedObject.GetIterator())
            {
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        outSerializedProperties.Add(serializedObject.FindProperty(iterator.name));
                    }
                    while (iterator.NextVisible(false));
                }
            }
        }

        private static Item CreatePropertyItem(SerializedProperty property)
        {
            var field = GetField(property);
            var item = new Item
            {
                Property = property,
                Field = field,
                Tab = PropertyUtility.GetAttribute<TabAttribute>(property),
                Foldout = PropertyUtility.GetAttribute<FoldoutAttribute>(property),
                Box = PropertyUtility.GetAttribute<BoxGroupAttribute>(property),
                Horizontal = PropertyUtility.GetAttribute<HorizontalGroupAttribute>(property),
                Vertical = PropertyUtility.GetAttribute<VerticalGroupAttribute>(property)
            };

            FillCommon(item, field, false);

            return item;
        }

        private static Item CreateMemberItem(MemberInfo member)
        {
            var item = new Item
            {
                Method = member as MethodInfo,
                Field = member as FieldInfo,
                Native = member as PropertyInfo,
                Tab = AttributeCache.Get<TabAttribute>(member),
                Foldout = AttributeCache.Get<FoldoutAttribute>(member),
                Box = AttributeCache.Get<BoxGroupAttribute>(member),
                Horizontal = AttributeCache.Get<HorizontalGroupAttribute>(member),
                Vertical = AttributeCache.Get<VerticalGroupAttribute>(member)
            };

            FillCommon(item, member, true);

            return item;
        }

        private static void FillCommon(Item item, MemberInfo member, bool readTitle)
        {
            if (member == null)
            {
                return;
            }

            item.Order = AttributeCache.Get<PropertyOrderAttribute>(member)?.Order ?? 0;
            item.Space = AttributeCache.Get<PropertySpaceAttribute>(member)?.Space ?? 0f;
            item.List = AttributeCache.Get<ListDrawerSettingsAttribute>(member);
            item.Table = AttributeCache.Get<TableListAttribute>(member);

            if (readTitle)
            {
                item.Title = AttributeCache.Get<TitleAttribute>(member)?.Title;
            }
        }

        private static int FirstLayer(Item item)
        {
            for (var i = 0; i < LayerCount; i++)
            {
                if (item.Key(i) != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FirstLayerFrom(Item item, int from)
        {
            for (var i = from; i < LayerCount; i++)
            {
                if (item.Key(i) != null)
                {
                    return i;
                }
            }

            return -1;
        }

        protected void DrawSerializedProperties()
        {
            serializedObject.Update();

            var items = new List<Item>();
            var grouped = new List<Item>();

            foreach (var property in _serializedProperties)
            {
                items.Add(CreatePropertyItem(property));
            }

            // Non-serialized fields and native properties follow the closest serialized field declared before them
            foreach (var field in _nonSerializedFields)
            {
                var item = CreateMemberItem(field);

                if (item.HasGroup)
                {
                    grouped.Add(item);

                    continue;
                }

                var index = -1;
                var bestToken = -1;

                for (var i = 0; i < items.Count; i++)
                {
                    var other = items[i].Field;

                    if (other != null && other.DeclaringType == field.DeclaringType
                        && other.MetadataToken < field.MetadataToken && other.MetadataToken > bestToken)
                    {
                        bestToken = other.MetadataToken;
                        index = i;
                    }
                }

                if (index < 0)
                {
                    items.Add(item);
                }
                else
                {
                    items.Insert(index + 1, item);
                }
            }

            foreach (var native in _nativeProperties)
            {
                AddMember(CreateMemberItem(native), items, grouped);
            }

            foreach (var method in _methods)
            {
                AddMember(CreateMemberItem(method), items, grouped);
            }

            SortByOrder(items);

            foreach (var item in grouped)
            {
                var layer = FirstLayer(item);
                var key = item.Key(layer);
                var index = -1;

                for (var n = items.Count - 1; n >= 0; n--)
                {
                    if (items[n].Key(layer) == key)
                    {
                        index = n;

                        break;
                    }
                }

                if (index < 0)
                {
                    var anchor = FindPrecedingFieldIndex(items, item.Field);

                    if (anchor >= 0)
                    {
                        items.Insert(anchor + 1, item);

                        continue;
                    }

                    index = -1;

                    for (var n = 0; n < items.Count; n++)
                    {
                        if (items[n].Order > item.Order)
                        {
                            index = n;

                            break;
                        }
                    }

                    if (index < 0)
                    {
                        items.Add(item);
                    }
                    else
                    {
                        items.Insert(index, item);
                    }
                }
                else
                {
                    items.Insert(index + 1, item);
                }
            }

            DrawLevel(items, 0);

            serializedObject.ApplyModifiedProperties();
        }

        // A new group starts right after the closest field declared before its first member
        private static int FindPrecedingFieldIndex(List<Item> items, FieldInfo field)
        {
            if (field == null)
            {
                return -1;
            }

            var index = -1;
            var bestToken = -1;

            for (var i = 0; i < items.Count; i++)
            {
                var other = items[i].Field;

                if (other != null && other.DeclaringType == field.DeclaringType
                    && other.MetadataToken < field.MetadataToken && other.MetadataToken > bestToken)
                {
                    bestToken = other.MetadataToken;
                    index = i;
                }
            }

            return index;
        }

        private static void AddMember(Item item, List<Item> items, List<Item> grouped)
        {
            if (item.HasGroup)
            {
                grouped.Add(item);
            }
            else
            {
                items.Add(item);
            }
        }

        private void DrawLevel(List<Item> items, int depth)
        {
            if (depth >= LayerCount)
            {
                foreach (var item in items)
                {
                    DrawItem(item);
                }

                return;
            }

            HashSet<string> handled = null;

            for (var i = 0; i < items.Count; i++)
            {
                var key = items[i].Key(depth);

                if (key == null)
                {
                    var deeper = FirstLayerFrom(items[i], depth + 1);

                    if (deeper < 0)
                    {
                        DrawItem(items[i]);

                        continue;
                    }

                    // Items without a key here but sharing a deeper group are merged into the same deeper group
                    var deeperKey = items[i].Key(deeper);

                    handled ??= new HashSet<string>();

                    if (!handled.Add($"{deeper}:{deeperKey}"))
                    {
                        continue;
                    }

                    var passThrough = new List<Item>();

                    foreach (var x in items)
                    {
                        if (x.Key(depth) == null && FirstLayerFrom(x, depth + 1) == deeper && x.Key(deeper) == deeperKey)
                        {
                            passThrough.Add(x);
                        }
                    }

                    if (AnyVisible(passThrough))
                    {
                        DrawLevel(passThrough, depth + 1);
                    }

                    continue;
                }

                // Groups with the same name are merged even when not declared contiguously
                handled ??= new HashSet<string>();

                if (!handled.Add(key))
                {
                    continue;
                }

                var run = new List<Item>();

                foreach (var x in items)
                {
                    if (x.Key(depth) == key)
                    {
                        run.Add(x);
                    }
                }

                if (!AnyVisible(run))
                {
                    continue;
                }

                DrawGroup(run, depth, key);
            }
        }

        // Stable in-place insertion sort (no LINQ allocations)
        private static void SortByOrder(List<Item> items)
        {
            for (var i = 1; i < items.Count; i++)
            {
                var current = items[i];
                var j = i - 1;

                while (j >= 0 && items[j].Order > current.Order)
                {
                    items[j + 1] = items[j];
                    j--;
                }

                items[j + 1] = current;
            }
        }

        private bool AnyVisible(List<Item> items)
        {
            foreach (var item in items)
            {
                if (IsVisible(item))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawGroup(List<Item> run, int depth, string key)
        {
            switch (depth)
            {
                case 0:
                    DrawTabs(run, depth, key);
                    break;
                case 1:
                    DrawFoldoutOrBox(run, depth, key);
                    break;
                case 2:
                    DrawHorizontal(run, depth);
                    break;
                default:
                    EditorGUILayout.BeginVertical();
                    DrawLevel(run, depth + 1);
                    EditorGUILayout.EndVertical();
                    break;
            }
        }

        private void DrawTabs(List<Item> run, int depth, string key)
        {
            var visible = new List<Item>(run.Count);
            var names = new List<string>();

            foreach (var item in run)
            {
                if (!IsVisible(item))
                {
                    continue;
                }

                visible.Add(item);

                if (!names.Contains(item.Tab.Name))
                {
                    names.Add(item.Tab.Name);
                }
            }

            var stateKey = $"MyTab.{target.GetType().Name}.{key}.{string.Join("|", names)}";
            var group = TabGroupUtility.Create(key, names, stateKey);

            foreach (var item in visible)
            {
                if (!string.IsNullOrEmpty(item.Tab.LabelMethod))
                {
                    group.SetLabel(item.Tab.Name, TabGroupUtility.ResolveLabel(target, item.Tab));
                }
            }

            group.Select(GUILayout.Toolbar(group.Selected, group.GetToolbarNames()));

            var tabItems = new List<Item>(visible.Count);
            var tabProperties = new List<SerializedProperty>(visible.Count);

            foreach (var item in visible)
            {
                if (item.Tab.Name == group.SelectedName)
                {
                    tabItems.Add(item);
                    tabProperties.Add(item.Property);
                }
            }

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            var widest = TabGroupUtility.MeasureLabelWidth(tabProperties, EditorGUIUtility.currentViewWidth);

            if (widest > 0f)
            {
                EditorGUIUtility.labelWidth = widest;
            }

            var boxRect = EditorGUILayout.BeginVertical(TabGroupUtility.ContentStyle);

            TabGroupUtility.DrawBox(boxRect);
            DrawLevel(tabItems, depth + 1);
            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private static void HandleGroupContextClick(Rect headerRect, List<Item> run)
        {
            if (Event.current.type != EventType.ContextClick || !headerRect.Contains(Event.current.mousePosition))
            {
                return;
            }

            GenericMenu menu = null;

            // The menu is only built on right-click, so drawing costs nothing
            foreach (var item in run)
            {
                var property = item.Property;

                if (property == null || item.Method != null || property.propertyType != SerializedPropertyType.Generic
                    || property.isArray || PropertyUtility.GetAttribute<InlinePropertyAttribute>(property) == null)
                {
                    continue;
                }

                menu ??= new GenericMenu();
                MyListDrawer.AddInlineCopyPaste(menu, property);
            }

            if (menu == null)
            {
                return;
            }

            menu.ShowAsContext();
            Event.current.Use();
        }

        private void DrawFoldoutOrBox(List<Item> run, int depth, string key)
        {
            var isFoldout = key.StartsWith("f:", StringComparison.Ordinal);
            var displayName = key.Substring(2);
            var name = _foldoutPrefix + displayName;

            if (isFoldout)
            {
                if (!_foldouts.ContainsKey(name))
                {
                    // Instance IDs change when a prefab is reopened, so the state is stored per type instead.
                    _foldouts[name] = new SavedBool($"MyInspector.Foldout.{target.GetType().FullName}.{name}", false);
                }

                var first = run[0].Property;
                var boolTitle = first != null && first.propertyType == SerializedPropertyType.Boolean
                    ? PropertyUtility.GetAttribute<BoolTitleAttribute>(first)
                    : null;

                var clean = false;

                foreach (var x in run)
                {
                    if (x.Foldout != null && x.Foldout.Clean)
                    {
                        clean = true;

                        break;
                    }
                }

                if (boolTitle != null)
                {
                    // The BoolTitle toggle acts as the foldout header; only the other fields are folded
                    if (!clean)
                    {
                        GUILayout.Space(InspectorStyle.FoldoutTopSpace);
                        BeginIndentedBox();
                    }

                    var rect = clean ? EditorGUILayout.GetControlRect() : GetBoxHeaderRect();
                    var content = new GUIContent(boolTitle.Title ?? first.displayName, first.tooltip);

                    HandleGroupContextClick(rect, run);
                    _foldouts[name].Value = EditorGUI.Foldout(new Rect(rect.x, rect.y, 5f, rect.height), _foldouts[name].Value, GUIContent.none, true);

                    var titleStyle = GetBoolTitleStyle();

                    EditorGUI.BeginProperty(rect, content, first);
                    first.boolValue = EditorGUI.ToggleLeft(new Rect(rect.x + InspectorStyle.BoolTitleToggleOffset, rect.y, rect.width - InspectorStyle.BoolTitleToggleOffset, rect.height), content, first.boolValue, titleStyle);
                    EditorGUI.EndProperty();

                    if (_foldouts[name].Value)
                    {
                        var enabled = first.boolValue;

                        // When unchecked, still list the grouped fields (greyed out) instead of hiding them
                        var previous = PropertyUtility.ForceVisible;
                        PropertyUtility.ForceVisible = previous || !enabled;

                        if (!clean)
                        {
                            EditorGUILayout.BeginVertical(GetBoxContentStyle());
                        }

                        EditorGUI.indentLevel++;

                        using (new EditorGUI.DisabledScope(!enabled))
                        {
                            DrawLevel(run.GetRange(1, run.Count - 1), depth + 1);
                        }

                        EditorGUI.indentLevel--;

                        if (!clean)
                        {
                            EditorGUILayout.EndVertical();
                        }

                        PropertyUtility.ForceVisible = previous;
                    }

                    if (!clean)
                    {
                        EndIndentedBox();
                        GUILayout.Space(InspectorStyle.FoldoutBottomSpace);
                    }

                    return;
                }

                if (clean)
                {
                    _foldouts[name].Value = EditorGUILayout.Foldout(_foldouts[name].Value, displayName, true);
                    HandleGroupContextClick(GUILayoutUtility.GetLastRect(), run);

                    if (_foldouts[name].Value)
                    {
                        DrawLevel(run, depth + 1);
                    }

                    return;
                }

                GUILayout.Space(InspectorStyle.FoldoutTopSpace);
                BeginIndentedBox();

                var headerRect = GetBoxHeaderRect();

                _foldouts[name].Value = EditorGUI.Foldout(headerRect, _foldouts[name].Value, displayName, true);
                HandleGroupContextClick(headerRect, run);

                if (_foldouts[name].Value)
                {
                    EditorGUILayout.BeginVertical(GetBoxContentStyle());
                    DrawLevel(run, depth + 1);
                    EditorGUILayout.EndVertical();
                }

                EndIndentedBox();
                GUILayout.Space(InspectorStyle.FoldoutBottomSpace);
            }
            else
            {
                MyEditorGUI.BeginBoxGroup_Layout(displayName);
                DrawLevel(run, depth + 1);
                MyEditorGUI.EndBoxGroup_Layout();
            }
        }

        private static Texture2D _headerTexture;
        private static Texture2D _contentTexture;
        private static GUIStyle _contentStyle;
        private static GUIStyle _boolTitleStyle;

        private static GUIStyle GetBoolTitleStyle()
        {
            if (_boolTitleStyle == null)
            {
                _boolTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = Mathf.Max(8, (EditorStyles.boldLabel.fontSize > 0 ? EditorStyles.boldLabel.fontSize : 12) - 1)
                };
            }

            return _boolTitleStyle;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };

            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }

        private static Rect GetBoxHeaderRect()
        {
            if (_headerTexture == null)
            {
                _headerTexture = MakeTexture(InspectorStyle.HeaderBackground);
            }

            var rect = GUILayoutUtility.GetRect(0f, InspectorStyle.BoxHeaderHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, _headerTexture);
            }

                        rect.x += 18f;
                        rect.width -= 18f;
                        rect.y += 4f;
            rect.height = EditorGUIUtility.singleLineHeight;

            return rect;
        }

        private static GUIStyle GetBoxContentStyle()
        {
            if (_contentTexture == null || _contentStyle == null)
            {
                _contentTexture = MakeTexture(InspectorStyle.ContentBackground);
                _contentStyle = new GUIStyle
                {
                    padding = new RectOffset(4, 4, 3, 3),
                    normal = { background = _contentTexture }
                };
            }

            return _contentStyle;
        }

        private void DrawHorizontal(List<Item> run, int depth)
        {
            var columns = new List<List<Item>>();

            foreach (var item in run)
            {
                var key = item.Key(depth + 1);
                var last = columns.Count > 0 ? columns[columns.Count - 1] : null;

                if (last != null && key != null && last[0].Key(depth + 1) == key)
                {
                    last.Add(item);
                }
                else
                {
                    columns.Add(new List<Item> { item });
                }
            }

            var totalWidth = EditorGUIUtility.currentViewWidth - 40f;
            var oldLabelWidth = EditorGUIUtility.labelWidth;

            EditorGUILayout.BeginHorizontal();

            foreach (var column in columns)
            {
                if (!AnyVisible(column))
                {
                    continue;
                }

                var width = column[0].Horizontal.Width;
                GUILayoutOption option;

                if (width <= 0f)
                {
                    option = GUILayout.ExpandWidth(true);
                }
                else
                {
                    option = GUILayout.Width(width < 1f ? width * totalWidth : width);
                }

                EditorGUILayout.BeginVertical(option);
                EditorGUIUtility.labelWidth = Mathf.Min(oldLabelWidth, Mathf.Max(50f, totalWidth / columns.Count * 0.45f));
                DrawLevel(column, depth + 1);
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool IsVisible(Item item)
        {
            return item.Property != null
                ? PropertyUtility.IsVisible(item.Property)
                : ButtonUtility.IsVisible(target, item.Member);
        }

        private void DrawItem(Item item)
        {
            var property = item.Property;

            if (property != null && property.name.Equals("m_Script", StringComparison.Ordinal))
            {
                using (new EditorGUI.DisabledScope(disabled: true))
                {
                    EditorGUILayout.PropertyField(property);
                }

                return;
            }

            if (!IsVisible(item))
            {
                return;
            }

            if (item.Space > 0f)
            {
                GUILayout.Space(item.Space);
            }

            var title = item.Title;

            if (string.IsNullOrEmpty(title) && property != null && item.Field != null && IsCustomDrawn(item))
            {
                // Custom drawn properties bypass PropertyField, so decorator drawers (Title) never run
                title = AttributeCache.Get<TitleAttribute>(item.Field)?.Title;
            }

            if (!string.IsNullOrEmpty(title))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                var line = EditorGUILayout.GetControlRect(false, InspectorStyle.TitleLineHeight);

                EditorGUI.DrawRect(line, InspectorStyle.HorizontalLine);
                GUILayout.Space(InspectorStyle.TitleBottomMargin);
            }

            if (property != null && item.Field != null && IsCustomDrawn(item))
            {
                // Same reason as Title: lists/containers bypass PropertyField, so InfoBox decorators never run
                var infos = AttributeCache.GetAll<InfoBoxAttribute>(item.Field);

                for (var i = 0; i < infos.Length; i++)
                {
                    var info = (InfoBoxAttribute)infos[i];

                    MyEditorGUI.HelpBox_Layout(ResolveText(info.Text), ToMessageType(info.Type));
                }
            }

            if (item.Method != null)
            {
                DrawButton(item.Method);
            }
            else if (item.Native != null)
            {
                MyEditorGUI.NativeProperty_Layout(serializedObject.targetObject, item.Native);
            }
            else if (property == null)
            {
                DrawNonSerializedField(item.Field);
            }
            else if (MyListDrawer.IsDictionary(property))
            {
                var rect = GetListRect(MyListDrawer.GetHeight(property));

                MyListDrawer.Field(rect, property);
            }
            else if (item.Table != null && property.isArray && property.propertyType != SerializedPropertyType.String
                && PropertyUtility.GetTargetObjectOfProperty(property) is System.Collections.IList tableList)
            {
                // Flush pending edits of previously drawn fields, otherwise the Update below would discard them
                serializedObject.ApplyModifiedProperties();
                MyTableDrawer.Draw(tableList, target, ObjectNames.NicifyVariableName(property.name),                 item.Table.IsReadOnly, null, property);
                serializedObject.Update();
            }
            else if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                DrawList(item);
            }
            else if (property.propertyType == SerializedPropertyType.ManagedReference
                && item.Field != null && property.managedReferenceValue == null)
            {
                DrawNullReference(property, item.Field.FieldType);
            }
            else if (MyListDrawer.IsContainer(property)
                && PropertyUtility.GetAttribute<InlinePropertyAttribute>(property) == null
                && PropertyUtility.GetAttribute<HideLabelAttribute>(property) == null)
            {
                DrawNested(property);
            }
            else if ((property.propertyType == SerializedPropertyType.Generic
                    || property.propertyType == SerializedPropertyType.ManagedReference)
                && MyListDrawer.IsContainer(property)
                && PropertyUtility.GetAttribute<InlinePropertyAttribute>(property) != null)
            {
                // PropertyField would draw the children with Unity's default layout, ignoring groups and nested containers
                DrawInlineChildren(property);
            }
            else
            {
                MyEditorGUI.PropertyField_Layout(property, includeChildren: true);
            }
        }

        // GUIStyle.none has no margins, so no automatic spacing is added between lists and foldouts
        private static Rect GetListRect(float height)
        {
            var rect = GUILayoutUtility.GetRect(0f, height, GUIStyle.none, GUILayout.ExpandWidth(true));

            return EditorGUI.IndentedRect(rect);
        }

        private static bool IsCustomDrawn(Item item)
        {
            var property = item.Property;

            if (MyListDrawer.IsDictionary(property))
            {
                return true;
            }

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                return true;
            }

            if (property.propertyType == SerializedPropertyType.ManagedReference && property.managedReferenceValue == null)
            {
                return true;
            }

            return MyListDrawer.IsContainer(property)
                && PropertyUtility.GetAttribute<InlinePropertyAttribute>(property) == null
                && PropertyUtility.GetAttribute<HideLabelAttribute>(property) == null;
        }

        private static bool HasGroupedChildren(SerializedProperty property)
        {
            if (!MyListDrawer.IsContainer(property))
            {
                return false;
            }

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            if (!iterator.NextVisible(true))
            {
                return false;
            }

            do
            {
                if (SerializedProperty.EqualContents(iterator, end))
                {
                    return false;
                }

                if (PropertyUtility.GetAttribute<TabAttribute>(iterator) != null
                    || PropertyUtility.GetAttribute<FoldoutAttribute>(iterator) != null
                    || PropertyUtility.GetAttribute<BoxGroupAttribute>(iterator) != null
                    || PropertyUtility.GetAttribute<HorizontalGroupAttribute>(iterator) != null
                    || PropertyUtility.GetAttribute<VerticalGroupAttribute>(iterator) != null)
                {
                    return true;
                }
            }
            while (iterator.NextVisible(false));

            return false;
        }

        private void DrawNested(SerializedProperty property)
        {
            GUILayout.Space(InspectorStyle.ListTopPadding);
            BeginIndentedBox();

            DrawNestedBox(property);

            EndIndentedBox();
            GUILayout.Space(InspectorStyle.FoldoutBottomSpace);
        }

        private readonly Stack<int> _boxIndents = new Stack<int>();

        // Layout boxes ignore EditorGUI.indentLevel, so the indent is applied as a left space and reset inside the box
        private void BeginIndentedBox()
        {
            var indent = EditorGUI.indentLevel;

            _boxIndents.Push(indent);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * InspectorStyle.IndentLength);
            EditorGUILayout.BeginVertical();
            EditorGUI.indentLevel = 0;
        }

        private void EndIndentedBox()
        {
            EditorGUILayout.EndVertical();
            DrawOutline(GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel = _boxIndents.Pop();
        }

        private static void DrawOutline(Rect rect)
        {
            if (Event.current.type == EventType.Repaint)
            {
                MyListDrawer.DrawOutline(rect);
            }
        }

        private void DrawInlineChildren(SerializedProperty property)
        {
            var items = new List<Item>();
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, end))
                    {
                        break;
                    }

                    items.Add(CreatePropertyItem(iterator.Copy()));
                }
                while (iterator.NextVisible(false));
            }

            var previousPrefix = _foldoutPrefix;

            _foldoutPrefix = property.propertyPath + ".";

            SortByOrder(items);
            // ForceVisible only reveals the fields of the disabled group, the conditions inside the nested class still apply
            var previousForceVisible = PropertyUtility.ForceVisible;

            PropertyUtility.ForceVisible = false;
            EditorGUI.indentLevel++;
            DrawLevel(items, 0);
            EditorGUI.indentLevel--;
            PropertyUtility.ForceVisible = previousForceVisible;

            _foldoutPrefix = previousPrefix;
        }

        private readonly Dictionary<string, string> _nestedKeys = new Dictionary<string, string>();

        private void DrawNestedBox(SerializedProperty property)
        {
            var headerRect = GetBoxHeaderRect();

            if (!_nestedKeys.TryGetValue(property.propertyPath, out var foldoutKey))
            {
                foldoutKey = $"MyInspector.Nested.{target.GetType().FullName}.{property.propertyPath}";
                _nestedKeys[property.propertyPath] = foldoutKey;
            }

            if (!_foldouts.TryGetValue(foldoutKey, out var saved))
            {
                saved = new SavedBool(foldoutKey, property.isExpanded);
                _foldouts[foldoutKey] = saved;
            }

            MyListDrawer.HandleContainerContextClick(headerRect, property);
            saved.Value = EditorGUI.Foldout(headerRect, saved.Value, PropertyUtility.GetLabel(property), true);
            property.isExpanded = saved.Value;

            if (!saved.Value)
            {
                return;
            }

            var items = new List<Item>();
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, end))
                    {
                        break;
                    }

                    items.Add(CreatePropertyItem(iterator.Copy()));
                }
                while (iterator.NextVisible(false));
            }

            var previousPrefix = _foldoutPrefix;

            _foldoutPrefix = property.propertyPath + ".";

            EditorGUILayout.BeginVertical(GetBoxContentStyle());
            SortByOrder(items);

            var previousForceVisible = PropertyUtility.ForceVisible;

            PropertyUtility.ForceVisible = false;
            DrawLevel(items, 0);
            PropertyUtility.ForceVisible = previousForceVisible;
            EditorGUILayout.EndVertical();

            _foldoutPrefix = previousPrefix;
        }

        private void DrawNullReference(SerializedProperty property, Type baseType)
        {
            var path = property.propertyPath;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(PropertyUtility.GetLabel(property));

                if (EditorGUILayout.DropdownButton(new GUIContent("Select type..."), FocusType.Keyboard))
                {
                    ShowTypeMenu(baseType, type =>
                    {
                        serializedObject.Update();
                        serializedObject.FindProperty(path).managedReferenceValue = Activator.CreateInstance(type);
                        serializedObject.ApplyModifiedProperties();
                    });
                }
            }
        }

        private static void ShowTypeMenu(Type baseType, Action<Type> onSelected)
        {
            var types = TypeCache.GetTypesDerivedFrom(baseType).ToList();

            if (!baseType.IsInterface)
            {
                types.Add(baseType);
            }

            types = types
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericType && !typeof(UnityEngine.Object).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToList();

            var menu = new GenericMenu();

            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No type available"));
            }

            foreach (var type in types)
            {
                var captured = type;

                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => onSelected(captured));
            }

            menu.ShowAsContext();
        }

        private void DrawList(Item item)
        {
            var rect = GetListRect(MyListDrawer.GetHeight(item.Property));

            MyListDrawer.Draw(rect, item.Property);
        }

        private void DrawNonSerializedField(FieldInfo field)
        {
            var infoBoxes = AttributeCache.GetAll<InfoBoxAttribute>(field);

            for (var i = 0; i < infoBoxes.Length; i++)
            {
                var info = (InfoBoxAttribute)infoBoxes[i];

                MyEditorGUI.HelpBox_Layout(ResolveText(info.Text), ToMessageType(info.Type));
            }

            var label = AttributeCache.Get<LabelAttribute>(field)?.Label ?? ObjectNames.NicifyVariableName(field.Name);

            if (AttributeCache.Get<HideLabelAttribute>(field) != null)
            {
                label = string.Empty;
            }

            var owner = field.IsStatic ? null : (object)target;
            var value = field.GetValue(owner);
            var readOnly = field.IsInitOnly || field.IsLiteral
                || AttributeCache.Get<ReadOnlyAttribute>(field) != null
                || !ButtonUtility.IsEnabled(target, field);

            var displayAsString = AttributeCache.Get<DisplayAsStringAttribute>(field);

            if (displayAsString != null)
            {
                DrawDisplayAsString(field, displayAsString, label, value);

                return;
            }

            if (value == null && !readOnly)
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    var picked = EditorGUILayout.ObjectField(label, null, field.FieldType, true);

                    if (picked != null)
                    {
                        Undo.RecordObject(target, "Change " + field.Name);
                        field.SetValue(owner, picked);
                        EditorUtility.SetDirty(target);
                        InvokeValueChanged(field);
                    }

                    return;
                }

                if (field.FieldType == typeof(string))
                {
                    value = string.Empty;
                }
            }

            if (value == null)
            {
                EditorGUILayout.LabelField(label, "null");

                return;
            }

            if (value is System.Collections.IList tableValue && AttributeCache.Get<TableListAttribute>(field) is { } tableAttribute)
            {
                MyTableDrawer.Draw(tableValue, target, label, readOnly || tableAttribute.IsReadOnly);

                return;
            }

            if (value is System.Collections.IList nonSerializedList)
            {
                DrawNonSerializedList(nonSerializedList, label, field.Name);

                return;
            }

            if (readOnly)
            {
                MyEditorGUI.Field_Layout(value, label);

                return;
            }

            var range = AttributeCache.Get<RangeAttribute>(field);

            if (range != null && (value is int || value is float))
            {
                var slid = value is int intValue
                    ? (object)EditorGUILayout.IntSlider(label, intValue, (int)range.min, (int)range.max)
                    : EditorGUILayout.Slider(label, (float)value, range.min, range.max);

                if (!Equals(value, slid))
                {
                    Undo.RecordObject(target, "Change " + field.Name);
                    field.SetValue(owner, slid);
                    EditorUtility.SetDirty(target);
                    InvokeValueChanged(field);
                    serializedObject.Update();
                }

                return;
            }

            if (!MyEditorGUI.EditableField_Layout(value, label, out var newValue) || Equals(value, newValue))
            {
                return;
            }

            Undo.RecordObject(target, "Change " + field.Name);
            field.SetValue(owner, newValue);
            EditorUtility.SetDirty(target);

            InvokeValueChanged(field);
            serializedObject.Update();
        }

        private void InvokeValueChanged(FieldInfo field)
        {
            foreach (var changed in field.GetCustomAttributes<OnValueChangedAttribute>(true))
            {
                var callback = target.GetType().GetMethod(
                    changed.CallbackName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);

                callback?.Invoke(target, null);
            }
        }

        private readonly Dictionary<string, bool> _nonSerializedListFoldouts = new Dictionary<string, bool>();

        private void DrawNonSerializedList(System.Collections.IList list, string label, string key)
        {
            _nonSerializedListFoldouts.TryGetValue(key, out var open);

            var headerRect = EditorGUILayout.GetControlRect(false, 22f);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(headerRect, InspectorStyle.HeaderBackground);
            }

            open = EditorGUI.Foldout(new Rect(headerRect.x + 14f, headerRect.y + 3f, headerRect.width - 100f, 16f), open, label, true);
            _nonSerializedListFoldouts[key] = open;

            var countStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontSize = 12 };

            if (list.Count == 0)
            {
                countStyle.normal.textColor = InspectorStyle.CountLabel;
            }

            EditorGUI.LabelField(new Rect(headerRect.xMax - 86f, headerRect.y, 80f, headerRect.height),
                list.Count == 0 ? "Empty" : list.Count == 1 ? "1 Element" : list.Count + " Elements", countStyle);

            if (!open)
            {
                return;
            }

            EditorGUI.indentLevel++;

            for (var i = 0; i < list.Count; i++)
            {
                var element = list[i];

                if (element == null)
                {
                    continue;
                }

                var elementType = element.GetType();

                if (elementType.IsPrimitive || elementType == typeof(string) || elementType.IsValueType || element is UnityEngine.Object)
                {
                    MyEditorGUI.Field_Layout(element, $"Element {i}");

                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var f in elementType.GetFields(flags))
                {
                    var visible = f.IsPublic
                        ? f.GetCustomAttribute<NonSerializedAttribute>() == null && f.GetCustomAttribute<HideInInspector>() == null
                        : f.GetCustomAttribute<SerializeField>() != null;

                    var fieldValue = f.GetValue(element);

                    if (!visible || fieldValue == null)
                    {
                        continue;
                    }

                    if (MyEditorGUI.EditableField_Layout(fieldValue, ObjectNames.NicifyVariableName(f.Name), out var newValue) && !Equals(fieldValue, newValue))
                    {
                        f.SetValue(element, newValue);
                        EditorUtility.SetDirty(target);
                    }
                }

                foreach (var method in elementType.GetMethods(flags))
                {
                    var button = method.GetCustomAttribute<ButtonAttribute>(true);

                    if (button == null || !method.GetParameters().All(p => p.IsOptional))
                    {
                        continue;
                    }

                    var text = string.IsNullOrEmpty(button.Text) ? ObjectNames.NicifyVariableName(method.Name) : button.Text;

                    if (GUILayout.Button(text))
                    {
                        method.Invoke(element, method.GetParameters().Select(p => p.DefaultValue).ToArray());
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawButton(MethodInfo method)
        {
            var infoBoxes = AttributeCache.GetAll<InfoBoxAttribute>(method);

            for (var i = 0; i < infoBoxes.Length; i++)
            {
                var info = (InfoBoxAttribute)infoBoxes[i];

                MyEditorGUI.HelpBox_Layout(ResolveText(info.Text), ToMessageType(info.Type));
            }

            MyEditorGUI.Button(serializedObject.targetObject, method);
        }

        private string ResolveText(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '$')
            {
                return text;
            }

            var name = text.Substring(1);
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            var type = target.GetType();
            var method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);

            if (method != null)
            {
                return method.Invoke(target, null)?.ToString() ?? string.Empty;
            }

            var property = type.GetProperty(name, flags);

            if (property != null)
            {
                return property.GetValue(target)?.ToString() ?? string.Empty;
            }

            var field = type.GetField(name, flags);

            return field != null ? field.GetValue(target)?.ToString() ?? string.Empty : text;
        }

        private static MessageType ToMessageType(EInfoBoxType type)
        {
            switch (type)
            {
                case EInfoBoxType.Warning: return MessageType.Warning;
                case EInfoBoxType.Error: return MessageType.Error;
                default: return MessageType.Info;
            }
        }

    }
}
