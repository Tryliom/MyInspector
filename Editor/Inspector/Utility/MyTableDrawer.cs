using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MyInspector.Editor
{
    /// <summary>
    /// Draws an IList as a table through reflection: one row per element, one column per member (or per group).
    /// Works for serialized and non serialized lists, including polymorphic elements.
    /// </summary>
    public static class MyTableDrawer
    {
        private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const float RowButtonWidth = InspectorStyle.TableRowButtonWidth;
        private const float RowPadding = InspectorStyle.TableRowPadding;
        private const float MinColumnWidth = InspectorStyle.TableMinColumnWidth;
        private const float NestedLabelWidth = InspectorStyle.TableNestedLabelWidth;
        private static int _currentRow;
        private const int MaxDepth = 4;

        private class Member
        {
            public string Name;
            public string CustomLabel;
            public string Title => string.IsNullOrEmpty(CustomLabel) ? ObjectNames.NicifyVariableName(Name) : CustomLabel;
            public int Order;
            public float Width;
            public bool Fit;
            public bool HideLabel;
            public FieldInfo Field;
            public PropertyInfo Property;
            public MethodInfo Method;
            public string ButtonText;
            public bool ReadOnly;
            public float LabelWidth = -1f;
        }

        private class Column
        {
            public string Key;
            public string Header;
            public float CellX;
            public float CellWidth;
            public bool Horizontal;
            public int Order;
            public int Index;
            public float Width;
            public bool Fit;
            public float ComputedWidth;
            public bool FillRest;
            public float Preferred = -1f;
            public float LabelWidth;
            public bool Flex;
            public List<Member> Members = new List<Member>();
        }

        private static readonly Dictionary<Type, List<Member>> MemberCache = new Dictionary<Type, List<Member>>();
        private static readonly Dictionary<int, List<Column>> ColumnCache = new Dictionary<int, List<Column>>();
        private static readonly Dictionary<FieldInfo, bool> PropertyDrawerCache = new Dictionary<FieldInfo, bool>();
        private static readonly Dictionary<(Column, Type), List<Member>> CellMembersCache = new Dictionary<(Column, Type), List<Member>>();
        private static readonly Dictionary<string, bool> Foldouts = new Dictionary<string, bool>();
        private static UnityEngine.Object _owner;
        private static SerializedProperty _listProperty;
        private static SerializedProperty _elementProperty;
        private static HashSet<Type> _drawerTypes;
        private static float _lastWidth;
        private static bool _fallbackExpanded = true;
        private static readonly Dictionary<(int, string), float> MeasuredHeights = new Dictionary<(int, string), float>();

        private static (int, string) MeasureKey(SerializedProperty property)
        {
            return (property.serializedObject.targetObject != null ? property.serializedObject.targetObject.GetHashCode() : 0, property.propertyPath);
        }

        private static void RepaintAll()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                window.Repaint();
            }
        }

        /// <summary>Returns the last measured height of a table drawn for this property, or an estimate before the first repaint.</summary>
        public static float GetHeight(SerializedProperty property)
        {
            if (MeasuredHeights.TryGetValue(MeasureKey(property), out var height))
            {
                return height;
            }

            return 32f + 22f + Mathf.Max(0, property.arraySize) * 26f + 4f;
        }

        public static void Draw(IList list, UnityEngine.Object owner, string label, bool isReadOnly, Action<bool> onChanged = null,
            SerializedProperty listProperty = null)
        {
            _owner = owner;
            _listProperty = listProperty;
            _elementProperty = null;

            var changed = false;
            var elementType = GetElementType(list);
            var canEdit = !isReadOnly && !list.IsReadOnly && !list.IsFixedSize;
            var outer = EditorGUILayout.BeginVertical();

            if (Event.current.type == EventType.Repaint && outer.width > 1f)
            {
                _lastWidth = outer.width;
            }

            var expanded = DrawHeader(list, label, elementType, canEdit, ref changed);

            var columns = BuildColumns(list);

            ComputeWidths(columns, canEdit);

            var headerRect = Rect.zero;

            if (expanded && list.Count > 0 && columns.Count > 0)
            {
                headerRect = DrawColumnHeaders(columns, canEdit);
            }

            var removeAt = -1;

            for (var i = 0; expanded && i < list.Count; i++)
            {
                if (DrawRow(list, i, columns, canEdit, isReadOnly, ref changed))
                {
                    removeAt = i;
                }
            }

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                DrawBorder(outer);
                DrawSeparators(columns, headerRect, outer);

                if (listProperty != null && outer.height > 1f)
                {
                    var key = MeasureKey(listProperty);

                    if (!MeasuredHeights.TryGetValue(key, out var previous) || Mathf.Abs(previous - outer.height) > 0.5f)
                    {
                        MeasuredHeights[key] = outer.height;
                        MyListDrawer.ClearHeightCache();
                        EditorApplication.delayCall += RepaintAll;
                    }
                }
            }

            if (removeAt >= 0)
            {
                Mark();
                list.RemoveAt(removeAt);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(owner);
                onChanged?.Invoke(true);
                GUI.changed = true;
            }
        }

        private const float CellSidePadding = InspectorStyle.TableCellSidePadding;
        private const float CellRightPadding = InspectorStyle.TableCellRightPadding;

        private static void DrawSeparators(List<Column> columns, Rect headerRect, Rect outer)
        {
            if (headerRect.height <= 0f)
            {
                return;
            }

            var color = InspectorStyle.TableBorder;

            for (var i = 1; i < columns.Count; i++)
            {
                if (columns[i].CellWidth <= 0f)
                {
                    continue;
                }

                EditorGUI.DrawRect(new Rect(columns[i].CellX - 3f, headerRect.y, 1f, outer.yMax - headerRect.y), color);
            }
        }

        private static bool DrawHeader(IList list, string label, Type elementType, bool canEdit, ref bool changed)
        {
            var rect = EditorGUILayout.GetControlRect(false, 22f + 10f);
            var line = new Rect(rect.x, rect.y + 5f, rect.width, 22f);

            if (Event.current.type == EventType.Repaint)
            {
                var isOpen = _listProperty != null ? _listProperty.isExpanded : _fallbackExpanded;
                var background = isOpen ? new Rect(rect.x - 2f, rect.y, rect.width + 4f, rect.height) : rect;

                EditorGUI.DrawRect(background, InspectorStyle.HeaderBackground);
            }

            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(list, -1, canEdit);
                Event.current.Use();
            }

            var addWidth = canEdit ? RowButtonWidth + 4f : 0f;
            var labelRect = new Rect(line.x + 18f, line.y, line.width - addWidth - 108f, line.height);
            var countRect = new Rect(line.xMax - addWidth - 86f, line.y, 80f, line.height);
            var expanded = _listProperty != null ? _listProperty.isExpanded : _fallbackExpanded;
            var countStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontSize = 12 };

            if (list.Count == 0)
            {
                countStyle.normal.textColor = InspectorStyle.CountLabel;
            }

            expanded = EditorGUI.Foldout(labelRect, expanded, label, true, EditorStyles.foldout);

            if (_listProperty != null)
            {
                _listProperty.isExpanded = expanded;
            }
            else
            {
                _fallbackExpanded = expanded;
            }

            EditorGUI.LabelField(countRect, list.Count == 0 ? "Empty" : list.Count == 1 ? "1 Item" : list.Count + " Items", countStyle);

            if (canEdit && GUI.Button(new Rect(line.xMax - RowButtonWidth, line.y + (line.height - 18f) * 0.5f, RowButtonWidth, 18f),
                    EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.iconButton))
            {
                AddElement(list, elementType, () =>
                {
                    EditorUtility.SetDirty(_owner);
                    GUI.changed = true;
                });
            }

            return expanded;
        }

        private static Type _clipboardType;
        private static string _clipboardJson;

        private static void Modify(Action action)
        {
            Mark();
            action();
            EditorUtility.SetDirty(_owner);
            _listProperty?.serializedObject.Update();
            RepaintAll();
        }

        private static object CloneElement(object element)
        {
            if (element == null)
            {
                return null;
            }

            var type = element.GetType();

            if (type.IsValueType || type == typeof(string))
            {
                return element;
            }

            var copy = CreateInstance(type);

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(element), copy);

            return copy;
        }

        private static void ShowContextMenu(IList list, int index, bool canEdit)
        {
            var menu = new GenericMenu();
            var elementType = GetElementType(list);

            if (index >= 0 && list[index] != null)
            {
                var element = list[index];
                var type = element.GetType();
                var json = type.IsValueType || type == typeof(string) ? null : JsonUtility.ToJson(element);

                menu.AddItem(new GUIContent("Copy"), false, () =>
                {
                    _clipboardType = type;
                    _clipboardJson = json;
                });
            }

            var canPaste = canEdit && _clipboardType != null && elementType.IsAssignableFrom(_clipboardType);

            if (canPaste && index >= 0)
            {
                menu.AddItem(new GUIContent("Paste"), false, () => Modify(() =>
                {
                    var pasted = CreateInstance(_clipboardType);

                    JsonUtility.FromJsonOverwrite(_clipboardJson ?? "{}", pasted);
                    list[index] = pasted;
                }));
            }
            else if (index >= 0)
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            if (canPaste && index < 0)
            {
                menu.AddItem(new GUIContent("Paste as new element"), false, () => Modify(() =>
                {
                    var pasted = CreateInstance(_clipboardType);

                    JsonUtility.FromJsonOverwrite(_clipboardJson ?? "{}", pasted);
                    list.Add(pasted);
                }));
            }

            if (canEdit && index >= 0)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Duplicate"), false, () => Modify(() => list.Insert(index + 1, CloneElement(list[index]))));
                menu.AddItem(new GUIContent("Delete"), false, () => Modify(() => list.RemoveAt(index)));

                if (index > 0)
                {
                    menu.AddItem(new GUIContent("Move up"), false, () => Modify(() => Swap(list, index, index - 1)));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move up"));
                }

                if (index < list.Count - 1)
                {
                    menu.AddItem(new GUIContent("Move down"), false, () => Modify(() => Swap(list, index, index + 1)));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move down"));
                }
            }

            if (canEdit)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Clear"), false, () => Modify(list.Clear));
            }

            menu.ShowAsContext();
        }

        private static void Swap(IList list, int a, int b)
        {
            (list[a], list[b]) = (list[b], list[a]);
        }

        private static GUIStyle _headerStyle;

        private static GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Normal, fontSize = 12, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleCenter };

        private static Rect DrawColumnHeaders(List<Column> columns, bool canEdit)
        {
            var rect = EditorGUILayout.BeginHorizontal();

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, InspectorStyle.ColumnHeaderBackground);
            }

            // Headers follow the real cell positions of the first row (previous repaint) so they stay aligned when cells overflow.
            var aligned = columns.All(c => c.CellWidth > 0f);

            if (aligned)
            {
                var labelRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, HeaderStyle, GUILayout.ExpandWidth(true));

                for (var i = 0; i < columns.Count; i++)
                {
                    var column = columns[i];

                    GUI.Label(new Rect(column.CellX, labelRect.y, column.CellWidth, labelRect.height), column.Header == "-" ? string.Empty : column.Header, HeaderStyle);
                }
            }
            else
            {
                GUILayout.Space(RowPadding);

                foreach (var column in columns)
                {
                    GUILayout.Label(column.Header == "-" ? string.Empty : column.Header, HeaderStyle, ColumnOptions(column));
                }

                if (canEdit)
                {
                    GUILayout.Space(RowButtonWidth);
                }
            }

            EditorGUILayout.EndHorizontal();

            return rect;
        }

        private static bool DrawRow(IList list, int index, List<Column> columns, bool canEdit, bool readOnly, ref bool changed)
        {
            var remove = false;
            var element = list[index];

            _currentRow = index;

            _elementProperty = _listProperty != null && _listProperty.isArray && index < _listProperty.arraySize
                ? _listProperty.GetArrayElementAtIndex(index)
                : null;

            var rect = EditorGUILayout.BeginVertical();

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, index % 2 == 0 ? InspectorStyle.EvenRow : InspectorStyle.OddRow);
            }

            GUILayout.Space(RowPadding);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(RowPadding);

            if (element == null)
            {
                GUILayout.Label("null", EditorStyles.miniLabel);
            }
            else if (IsSimple(element.GetType()))
            {
                var value = element;

                if (DrawValue(ref value, string.Empty, readOnly, 0))
                {
                    list[index] = value;
                    changed = true;
                }
            }
            else
            {
                var type = element.GetType();

                foreach (var column in columns)
                {
                    DrawCell(column, element, type, readOnly, ref changed);
                }

                if (element.GetType().IsValueType)
                {
                    list[index] = element;
                }
            }

            if (canEdit)
            {
                // Reserve the button slot in the layout, the button itself is drawn at the row's right edge below.
                GUILayout.Space(RowButtonWidth);
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(RowPadding);
            EditorGUILayout.EndVertical();

            var rowRect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(list, index, canEdit);
                Event.current.Use();
            }

            if (canEdit)
            {
                var removeStyle = new GUIStyle(EditorStyles.iconButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                var buttonRect = new Rect(rowRect.xMax - RowButtonWidth, rowRect.y + (rowRect.height - 20f) * 0.5f, RowButtonWidth - 2f, 20f);

                if (GUI.Button(buttonRect, "x", removeStyle))
                {
                    remove = true;
                }
            }

            return remove;
        }

        private static void DrawCell(Column column, object element, Type type, bool readOnly, ref bool changed)
        {
            var cellKey = (column, type);

            if (!CellMembersCache.TryGetValue(cellKey, out var members))
            {
                members = column.Members.Where(m => IsMemberOf(m, type)).ToList();
                CellMembersCache[cellKey] = members;
            }

            var cellRect = column.Horizontal
                ? EditorGUILayout.BeginHorizontal(ColumnOptions(column))
                : EditorGUILayout.BeginVertical(ColumnOptions(column));

            if (Event.current.type == EventType.Repaint && (_currentRow == 0 || column.CellWidth <= 0f))
            {
                column.CellX = cellRect.x;
                column.CellWidth = cellRect.width;
            }

            var showLabels = members.Count > 1 && !column.Horizontal;

            GUILayout.BeginHorizontal();
            GUILayout.Space(CellSidePadding);

            if (column.Horizontal)
            {
                EditorGUILayout.BeginHorizontal();
            }
            else
            {
                EditorGUILayout.BeginVertical();
            }

            foreach (var member in members)
            {
                if (DrawMember(member, element, showLabels && !member.HideLabel, readOnly, column.LabelWidth, column.FillRest))
                {
                    changed = true;
                }
            }

            if (column.Horizontal)
            {
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(CellRightPadding);
            GUILayout.EndHorizontal();

            if (column.Horizontal)
            {
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.EndVertical();
            }
        }

        private static bool DrawMember(Member member, object element, bool showLabel, bool readOnly, float labelWidth, bool fillRest = false)
        {
            if (labelWidth <= 0f)
            {
                labelWidth = NestedLabelWidth;
            }

            var memberInfo = member.Method ?? (MemberInfo)member.Field ?? member.Property;

            // Conditions are evaluated against the table element, not the inspected object.
            if (!ButtonUtility.IsVisible(element, memberInfo))
            {
                return false;
            }

            if (!ButtonUtility.IsEnabled(element, memberInfo))
            {
                readOnly = true;
            }

            var label = showLabel ? member.Title : string.Empty;

            if (member.Method != null)
            {
                var text = string.IsNullOrEmpty(member.ButtonText) ? member.Title : member.ButtonText;

                if (GUILayout.Button(text))
                {
                    member.Method.Invoke(element, member.Method.GetParameters().Select(p => p.DefaultValue).ToArray());
                }

                return false;
            }

            var value = member.Field != null ? member.Field.GetValue(element) : member.Property.GetValue(element);
            var memberReadOnly = readOnly || member.ReadOnly;

            if (!memberReadOnly && member.Field != null && _elementProperty != null && (value is IDictionary || value is IList) && !(value is Array))
            {
                var container = _elementProperty.FindPropertyRelative(member.Name);

                if (container != null)
                {
                    var owner = _owner;
                    var listProperty = _listProperty;
                    var elementProperty = _elementProperty;

                    var height = MyListDrawer.GetHeight(container);
                    var rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));

                    EditorGUI.BeginChangeCheck();
                    MyListDrawer.Field(rect, container, new GUIContent(label));

                    if (EditorGUI.EndChangeCheck())
                    {
                        Mark();
                        container.serializedObject.ApplyModifiedProperties();
                    }

                    _owner = owner;
                    _listProperty = listProperty;
                    _elementProperty = elementProperty;

                    return false;
                }
            }

            if (!memberReadOnly && member.Field != null && _elementProperty != null && UsesPropertyDrawer(member.Field))
            {
                var property = _elementProperty.FindPropertyRelative(member.Name);

                if (property != null)
                {
                    var previousLabelWidth = EditorGUIUtility.labelWidth;

                    EditorGUIUtility.labelWidth = string.IsNullOrEmpty(label) ? 1f : labelWidth;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(property, new GUIContent(label), true);

                    var propertyChanged = EditorGUI.EndChangeCheck();

                    EditorGUIUtility.labelWidth = previousLabelWidth;

                    if (propertyChanged)
                    {
                        Mark();
                        property.serializedObject.ApplyModifiedProperties();
                    }

                    return false;
                }
            }

            if (!DrawValue(ref value, label, memberReadOnly, 0, member.Field?.FieldType ?? member.Property.PropertyType, labelWidth))
            {
                return false;
            }

            Mark();
            member.Field?.SetValue(element, value);

            return true;
        }

        /// <summary>Draws any value. Returns true when the value changed and must be written back by the caller.</summary>
        private static bool DrawValue(ref object value, string label, bool readOnly,         int depth, Type declaredType = null, float labelWidth = NestedLabelWidth)
        {
            var type = value?.GetType() ?? declaredType;
            var oldLabelWidth = EditorGUIUtility.labelWidth;

                    EditorGUIUtility.labelWidth = string.IsNullOrEmpty(label) ? 1f : labelWidth;

            try
            {
                if (value == null)
                {
                    return DrawNull(ref value, label, readOnly, type);
                }

                if (value is IDictionary dictionary)
                {
                    return DrawDictionary(dictionary, label, readOnly, depth);
                }

                if (value is IList nested && !(value is Array && readOnly))
                {
                    return DrawNestedList(nested, label, readOnly, depth);
                }

                if (!IsSimple(type))
                {
                    return depth < MaxDepth && DrawObject(value, label, readOnly, depth);
                }

                if (readOnly)
                {
                    MyEditorGUI.Field_Layout(value, label);

                    return false;
                }

                if (MyEditorGUI.EditableField_Layout(value, label, out var newValue) && !Equals(value, newValue))
                {
                    value = newValue;

                    return true;
                }

                return false;
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        private static bool DrawNull(ref object value, string label, bool readOnly, Type type)
        {
            if (readOnly || type == null)
            {
                EditorGUILayout.LabelField(label, "null");

                return false;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var picked = EditorGUILayout.ObjectField(label, null, type, true);

                if (picked != null)
                {
                    value = picked;

                    return true;
                }

                return false;
            }

            if (type == typeof(string))
            {
                var text = EditorGUILayout.TextField(label, string.Empty);

                if (!string.IsNullOrEmpty(text))
                {
                    value = text;

                    return true;
                }

                return false;
            }

            EditorGUILayout.LabelField(label, "null");

            return false;
        }

        private static bool DrawObject(object obj, string label, bool readOnly, int depth)
        {
            var changed = false;
            var members = GetMembers(obj.GetType()).Where(m => m.Method == null).ToList();

            if (!string.IsNullOrEmpty(label) && members.Count > 0)
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            }

            var nestedLabelWidth = members.Where(m => !m.HideLabel).Select(MeasureLabel).DefaultIfEmpty(NestedLabelWidth).Max();

            string openGroup = null;

            foreach (var member in members)
            {
                var memberInfo = member.Field ?? (MemberInfo)member.Property;
                var group = memberInfo.GetCustomAttribute<HorizontalGroupAttribute>(true)?.Name;

                if (group != openGroup)
                {
                    if (openGroup != null)
                    {
                        EditorGUILayout.EndHorizontal();
                    }

                    openGroup = group;

                    if (openGroup != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                    }
                }

                if (!ButtonUtility.IsVisible(obj, memberInfo))
                {
                    continue;
                }

                var value = member.Field != null ? member.Field.GetValue(obj) : member.Property.GetValue(obj);
                var memberLabel = member.HideLabel ? string.Empty : member.Title;
                var enabled = ButtonUtility.IsEnabled(obj, memberInfo);
                var labelWidth = group != null ? MeasureLabel(member) : nestedLabelWidth;

                if (!DrawValue(ref value, memberLabel, readOnly || member.ReadOnly || !enabled, depth + 1, member.Field?.FieldType ?? member.Property.PropertyType, labelWidth))
                {
                    continue;
                }

                Mark();
                member.Field?.SetValue(obj, value);
                changed = true;
            }

            if (openGroup != null)
            {
                EditorGUILayout.EndHorizontal();
            }

            return changed;
        }

        private static bool DrawNestedList(IList list, string label, bool readOnly, int depth)
        {
            var changed = false;
            var elementType = GetElementType(list);
            var canEdit = !readOnly && !list.IsReadOnly && !list.IsFixedSize;
            var removeAt = -1;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(label) ? "List" : label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(list.Count + (list.Count == 1 ? " Item" : " Items"), EditorStyles.miniLabel, GUILayout.Width(75f));

            if (canEdit && GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.iconButton, GUILayout.Width(RowButtonWidth)))
            {
                AddElement(list, elementType, () =>
                {
                    EditorUtility.SetDirty(_owner);
                    GUI.changed = true;
                });
            }

            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < list.Count; i++)
            {
                var element = list[i];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                if (DrawValue(ref element, string.Empty, readOnly, depth + 1, elementType))
                {
                    Mark();
                    list[i] = element;
                    changed = true;
                }

                EditorGUILayout.EndVertical();

                if (canEdit && GUILayout.Button("X", GUILayout.Width(RowButtonWidth - 2f)))
                {
                    removeAt = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            if (removeAt >= 0)
            {
                Mark();
                list.RemoveAt(removeAt);
                changed = true;
            }

            return changed;
        }

        private static void AddElement(IList list, Type elementType, Action onAdded)
        {
            if (elementType == null)
            {
                return;
            }

            var candidates = new List<Type>();

            if (!elementType.IsAbstract && !elementType.IsInterface)
            {
                candidates.Add(elementType);
            }

            if (!elementType.IsValueType && elementType != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(elementType))
            {
                candidates.AddRange(TypeCache.GetTypesDerivedFrom(elementType)
                    .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && !typeof(UnityEngine.Object).IsAssignableFrom(t)));
            }

            if (candidates.Count == 0)
            {
                Mark();
                list.Add(null);
                onAdded();

                return;
            }

            if (candidates.Count == 1)
            {
                Mark();
                list.Add(CreateInstance(candidates[0]));
                onAdded();

                return;
            }

            var menu = new GenericMenu();

            foreach (var candidate in candidates)
            {
                var captured = candidate;

                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(captured.Name)), false, () =>
                {
                    Mark();
                    list.Add(CreateInstance(captured));
                    onAdded();
                });
            }

            menu.ShowAsContext();
        }

        private static object CreateInstance(Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }

            try
            {
                return Activator.CreateInstance(type, true);
            }
            catch (Exception)
            {
                return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            }
        }

        private static List<Column> BuildColumns(IList list)
        {
            var types = new HashSet<Type>();

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && !IsSimple(list[i].GetType()))
                {
                    types.Add(list[i].GetType());
                }
            }

            var typesKey = 0;

            foreach (var type in types)
            {
                typesKey += type.GetHashCode() * 31 + 17;
            }

            if (ColumnCache.TryGetValue(typesKey, out var cachedColumns))
            {
                return cachedColumns;
            }

            var built = BuildColumns(types);

            ColumnCache[typesKey] = built;

            return built;
        }

        private static List<Column> BuildColumns(HashSet<Type> types)
        {
            var columns = new Dictionary<string, Column>();

            foreach (var type in types)
            {
                foreach (var member in GetMembers(type))
                {
                    var key = GetColumnKey(member, out var header, out var horizontal);

                    if (!columns.TryGetValue(key, out var column))
                    {
                        column = new Column { Key = key, Header = header, Horizontal = horizontal, Index = columns.Count, Order = member.Order };
                        columns[key] = column;
                    }

                    var declaring = member.Field?.DeclaringType ?? member.Property?.DeclaringType ?? member.Method.DeclaringType;

                    if (column.Members.All(m => m.Name != member.Name || (m.Field?.DeclaringType ?? m.Property?.DeclaringType ?? m.Method.DeclaringType) != declaring))
                    {
                        column.Members.Add(member);
                    }

                    column.Order = Math.Min(column.Order, member.Order);

                    if (member.Width > 0f)
                    {
                        column.Width = member.Width;
                    }

                    column.Fit |= member.Fit;
                }
            }

            foreach (var column in columns.Values)
            {
                column.Members = column.Members.OrderBy(m => m.Order).ToList();
            }

            return columns.Values.OrderBy(c => c.Order).ThenBy(c => c.Index).ToList();
        }

        private static string GetColumnKey(Member member, out string header, out bool horizontal)
        {
            var info = (MemberInfo)member.Field ?? (MemberInfo)member.Property ?? member.Method;
            var horizontalGroup = info.GetCustomAttribute<HorizontalGroupAttribute>(true);
            var verticalGroup = info.GetCustomAttribute<VerticalGroupAttribute>(true);

            horizontal = horizontalGroup != null;

            if (horizontalGroup != null)
            {
                header = horizontalGroup.Name;

                return "h:" + horizontalGroup.Name;
            }

            if (verticalGroup != null)
            {
                header = verticalGroup.Name;

                return "v:" + verticalGroup.Name;
            }

            header = member.Method != null ? string.Empty : member.Title;

            return "m:" + member.Name;
        }

        private static List<Member> GetMembers(Type type)
        {
            if (MemberCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var members = new List<Member>();
            var hierarchy = new List<Type>();

            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                hierarchy.Insert(0, t);
            }

            foreach (var t in hierarchy)
            {
                foreach (var field in t.GetFields(MemberFlags))
                {
                    var show = field.GetCustomAttribute<ShowNonSerializedFieldAttribute>(true) != null;
                    var serialized = field.IsPublic
                        ? field.GetCustomAttribute<NonSerializedAttribute>() == null
                        : field.GetCustomAttribute<SerializeField>() != null || field.GetCustomAttribute<SerializeReference>() != null;

                    if (field.Name.Contains("<") || field.GetCustomAttribute<HideInInspector>() != null || !(serialized || show))
                    {
                        continue;
                    }

                    members.Add(CreateMember(field, field.Name, !serialized || field.IsInitOnly || field.IsLiteral));
                }

                foreach (var property in t.GetProperties(MemberFlags))
                {
                    if (property.GetCustomAttribute<ShowNativePropertyAttribute>(true) == null || !property.CanRead || property.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    members.Add(CreateMember(property, property.Name, true));
                }

                foreach (var method in t.GetMethods(MemberFlags))
                {
                    var button = method.GetCustomAttribute<ButtonAttribute>(true);

                    if (button == null || !method.GetParameters().All(p => p.IsOptional))
                    {
                        continue;
                    }

                    var member = CreateMember(method, method.Name, false);

                    member.Method = method;
                    member.ButtonText = button.Text;
                    members.Add(member);
                }
            }

            MemberCache[type] = members;

            return members;
        }

        private static Member CreateMember(MemberInfo info, string name, bool readOnly)
        {
            return new Member
            {
                Name = name,
                CustomLabel = info.GetCustomAttribute<LabelAttribute>(true)?.Label,
                Field = info as FieldInfo,
                Property = info as PropertyInfo,
                Order = info.GetCustomAttribute<PropertyOrderAttribute>(true)?.Order ?? 0,
                Width = info.GetCustomAttribute<TableColumnWidthAttribute>(true)?.Width ?? 0f,
                Fit = info.GetCustomAttribute<TableColumnFitAttribute>(true) != null,
                HideLabel = info.GetCustomAttribute<HideLabelAttribute>(true) != null,
                ReadOnly = readOnly || info.GetCustomAttribute<ReadOnlyAttribute>(true) != null
            };
        }

        private static bool IsMemberOf(Member member, Type type)
        {
            var declaring = (member.Field?.DeclaringType) ?? member.Property?.DeclaringType ?? member.Method.DeclaringType;

            return declaring.IsAssignableFrom(type);
        }

        private static GUILayoutOption[] ColumnOptions(Column column)
        {
            if (column.FillRest)
            {
                return new[] { GUILayout.MinWidth(column.ComputedWidth), GUILayout.ExpandWidth(true) };
            }

            return new[] { GUILayout.Width(column.ComputedWidth), GUILayout.ExpandWidth(false) };
        }

        /// <summary>Gives every column an explicit width so the header and the rows always line up.</summary>
        private static void ComputeWidths(List<Column> columns, bool canEdit)
        {
            var available = _lastWidth > 1f ? _lastWidth : EditorGUIUtility.currentViewWidth - 40f;
            var spacing = columns.Count * 4f + 10f + (canEdit ? RowButtonWidth : 0f);
            var total = spacing;
            var autoCount = 0;
            var flexCount = 0;
            var autoPreferred = 0f;

            foreach (var column in columns)
            {
                if (column.Fit && column.Width <= 0f)
                {
                    EnsurePreferred(column);
                    column.Width = column.Preferred;
                }

                if (column.Width > 0f)
                {
                    total += column.Width;

                    continue;
                }

                EnsurePreferred(column);
                total += column.Preferred;
                autoPreferred += column.Preferred;
                autoCount++;

                if (column.Flex)
                {
                    flexCount++;
                }
            }

            var extra = available - total;
            Column fillColumn = null;

            foreach (var column in columns)
            {
                column.FillRest = false;

                if (column.Width <= 0f && (column.Flex || flexCount == 0))
                {
                    fillColumn = column;
                }
            }

            if (fillColumn != null)
            {
                fillColumn.FillRest = true;
            }

            foreach (var column in columns)
            {
                if (column.Width > 0f)
                {
                    column.ComputedWidth = column.Width;

                    continue;
                }

                if (extra >= 0f)
                {
                    var share = flexCount > 0 ? (column.Flex ? extra / flexCount : 0f) : extra / Mathf.Max(1, autoCount);

                    column.ComputedWidth = column.Preferred + share;
                }
                else
                {
                    column.ComputedWidth = Mathf.Max(MinColumnWidth * 0.5f, column.Preferred + extra * column.Preferred / Mathf.Max(1f, autoPreferred));
                }
            }
        }

        private static void EnsurePreferred(Column column)
        {
            if (column.Preferred >= 0f)
            {
                return;
            }

            var labeled = !column.Horizontal && column.Members.Count > 1;
            var preferred = 0f;

            column.LabelWidth = 0f;

            foreach (var member in column.Members)
            {
                if (labeled && !member.HideLabel)
                {
                    column.LabelWidth = Mathf.Max(column.LabelWidth, MeasureLabel(member));
                }
            }

            foreach (var member in column.Members)
            {
                var fieldWidth = PreferredFieldWidth(member, out var flex);
                var labelWidth = labeled && !member.HideLabel ? column.LabelWidth : 0f;

                column.Flex |= flex;

                if (column.Horizontal)
                {
                    preferred += fieldWidth + 4f;
                }
                else
                {
                    preferred = Mathf.Max(preferred, labelWidth + fieldWidth);
                }
            }

            var header = string.IsNullOrEmpty(column.Header) ? 0f : EditorStyles.boldLabel.CalcSize(new GUIContent(column.Header)).x + 8f;

            column.Preferred = Mathf.Max(preferred, header, 24f);
        }

        private static float MeasureLabel(Member member)
        {
            if (member.LabelWidth < 0f)
            {
                member.LabelWidth = EditorStyles.label.CalcSize(new GUIContent(member.Title)).x + 14f;
            }

            return member.LabelWidth;
        }

        private static float PreferredFieldWidth(Member member, out bool flex)
        {
            flex = false;

            if (member.Method != null)
            {
                return EditorStyles.miniButton.CalcSize(new GUIContent(string.IsNullOrEmpty(member.ButtonText) ? ObjectNames.NicifyVariableName(member.Name) : member.ButtonText)).x + 8f;
            }

            var type = member.Field?.FieldType ?? member.Property.PropertyType;

            return PreferredTypeWidth(type, out flex, 0);
        }

        private static float PreferredTypeWidth(Type type, out bool flex, int depth)
        {
            flex = false;
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool))
            {
                return 20f;
            }

            if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong))
            {
                return 46f;
            }

            if (type.IsEnum)
            {
                var longest = 0f;

                foreach (var name in Enum.GetNames(type))
                {
                    longest = Mathf.Max(longest, EditorStyles.popup.CalcSize(new GUIContent(ObjectNames.NicifyVariableName(name))).x);
                }

                return Mathf.Clamp(longest + 4f, 50f, 140f);
            }

            flex = true;

            if (type == typeof(string))
            {
                return 100f;
            }

            if (type == typeof(Color))
            {
                return 60f;
            }

            if (typeof(System.Collections.IDictionary).IsAssignableFrom(type))
            {
                return 320f;
            }

            // Plain serializable struct/class: size it from its own fields instead of a generic default.
            if (depth < 2 && !HasCustomDrawer(type) && (type.IsValueType || type.IsSerializable) && !typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsPrimitive)
            {
                var nested = 0f;
                var nestedFlex = false;
                var count = 0;

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.IsNotSerialized || (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null))
                    {
                        continue;
                    }

                    nested = Mathf.Max(nested, PreferredTypeWidth(field.FieldType, out var childFlex, depth + 1));
                    nestedFlex |= childFlex;
                    count++;
                }

                if (count > 0)
                {
                    flex = nestedFlex;

                    return nested;
                }
            }

            return 200f;
        }

        private static bool UsesPropertyDrawer(FieldInfo field)
        {
            if (PropertyDrawerCache.TryGetValue(field, out var cached))
            {
                return cached;
            }

            var result = ComputeUsesPropertyDrawer(field);

            PropertyDrawerCache[field] = result;

            return result;
        }

        private static bool ComputeUsesPropertyDrawer(FieldInfo field)
        {
            if (field.GetCustomAttributes(typeof(PropertyAttribute), true).Length > 0)
            {
                return true;
            }

            var type = field.FieldType;

            if (IsSimple(type) || typeof(IList).IsAssignableFrom(type))
            {
                return false;
            }

            return HasCustomDrawer(type);
        }

        private static bool HasCustomDrawer(Type type)
        {
            if (_drawerTypes == null)
            {
                _drawerTypes = new HashSet<Type>();

                var typeField = typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.Instance | BindingFlags.NonPublic);

                foreach (var drawer in TypeCache.GetTypesDerivedFrom<PropertyDrawer>())
                {
                    foreach (var attribute in drawer.GetCustomAttributes<CustomPropertyDrawer>(true))
                    {
                        if (typeField?.GetValue(attribute) is Type target)
                        {
                            _drawerTypes.Add(target);
                        }
                    }
                }
            }

            return _drawerTypes.Contains(type);
        }

        private static bool DrawDictionary(IDictionary dictionary, string label, bool readOnly, int depth)
        {
            var changed = false;
            var type = dictionary.GetType();
            var args = GetDictionaryArguments(type);
            var canEdit = !readOnly;
            var keys = dictionary.Keys.Cast<object>().ToList();
            object removeKey = null;
            var hasRemove = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(label) ? "Dictionary" : label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(keys.Count + (keys.Count == 1 ? " Item" : " Items"), EditorStyles.miniLabel, GUILayout.Width(75f));

            if (canEdit && args != null && GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.iconButton, GUILayout.Width(RowButtonWidth)))
            {
                var newKey = CreateUnusedKey(dictionary, args[0]);

                if (newKey != null)
                {
                    Mark();
                    dictionary[newKey] = args[1] == typeof(string) ? string.Empty : (args[1].IsValueType ? Activator.CreateInstance(args[1]) : null);
                    EditorUtility.SetDirty(_owner);
                    GUI.changed = true;
                    changed = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            foreach (var key in keys)
            {
                EditorGUILayout.BeginHorizontal();

                var keyText = key == null ? "null" : ObjectNames.NicifyVariableName(key.ToString());

                EditorGUILayout.LabelField(keyText, GUILayout.Width(Mathf.Max(40f, NestedLabelWidth)));

                var value = dictionary[key];

                EditorGUILayout.BeginVertical();

                if (DrawValue(ref value, string.Empty, readOnly, depth + 1, args?[1]))
                {
                    Mark();
                    dictionary[key] = value;
                    changed = true;
                }

                EditorGUILayout.EndVertical();

                if (canEdit && GUILayout.Button("X", GUILayout.Width(RowButtonWidth - 2f)))
                {
                    removeKey = key;
                    hasRemove = true;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            if (hasRemove)
            {
                Mark();
                dictionary.Remove(removeKey);
                changed = true;
            }

            return changed;
        }

        private static Type[] GetDictionaryArguments(Type type)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    return type.GetGenericArguments();
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object CreateUnusedKey(IDictionary dictionary, Type keyType)
        {
            if (keyType.IsEnum)
            {
                foreach (var value in Enum.GetValues(keyType))
                {
                    if (!dictionary.Contains(value))
                    {
                        return value;
                    }
                }

                return null;
            }

            if (keyType == typeof(string))
            {
                return dictionary.Contains(string.Empty) ? null : string.Empty;
            }

            if (keyType.IsValueType)
            {
                var key = Activator.CreateInstance(keyType);

                return dictionary.Contains(key) ? null : key;
            }

            return null;
        }

        private static bool IsSimple(Type type)
        {
            return type != null && (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type)
                || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Vector2Int)
                || type == typeof(Vector3Int) || type == typeof(Color) || type == typeof(Bounds) || type == typeof(Rect)
                || type == typeof(RectInt) || type == typeof(decimal));
        }

        private static Type GetElementType(IList list)
        {
            var type = list.GetType();

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            return type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
        }

        private static void Mark()
        {
            if (_owner != null)
            {
                Undo.RecordObject(_owner, "Edit Table");
            }
        }

        private static void DrawBorder(Rect rect)
        {
            var color = InspectorStyle.NestedBorder;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }
    }
}
