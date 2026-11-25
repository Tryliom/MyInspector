using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MyInspector.Editor
{
    /// <summary>
    /// Draws arrays and lists with an Odin-like look: a header with the element count and a "+" button,
    /// and a "x" button on each element. Rect based, so it can be used from inspectors and property drawers.
    /// </summary>
    public static class MyListDrawer
    {
        private const float HeaderHeight = InspectorStyle.ListHeaderHeight;
        private const float ButtonWidth = InspectorStyle.ListButtonWidth;
        private const float Spacing = InspectorStyle.ListSpacing;
        private const float ElementIndent = InspectorStyle.ListElementIndent;
        private const float TopPadding = InspectorStyle.ListTopPadding;
        private const float InnerPadding = InspectorStyle.ListInnerPadding;
        private const float HeaderPadding = InspectorStyle.ListHeaderPadding;
        private const float ItemPadding = InspectorStyle.ListItemPadding;
        private const float BottomPadding = InspectorStyle.ListBottomPadding;

        private static Color HeaderColor => InspectorStyle.ListHeaderTint;
        private static Color BorderColor => InspectorStyle.Border;
        private static Color EvenRowColor => InspectorStyle.EvenRow;
        private static Color OddRowColor => InspectorStyle.OddRow;

        private static string _dragKey;
        private static int _dragIndex;
        private static int _dragTarget;

        private static GUIStyle _handleStyle;
        private static GUIStyle _removeStyle;

        private static GUIStyle HandleStyle => _handleStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = InspectorStyle.CountLabel }
        };

        private static GUIStyle RemoveStyle => _removeStyle ??= new GUIStyle(EditorStyles.iconButton)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(0, 0, 0, 0)
        };

        public static bool IsList(SerializedProperty property)
        {
            return property.isArray && property.propertyType != SerializedPropertyType.String;
        }

        public static bool IsDictionary(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.Generic || property.isArray)
            {
                return false;
            }

            var key = (property.serializedObject.GetHashCode(), property.propertyPath);

            if (DictionaryFlags.TryGetValue(key, out var flag))
            {
                return flag;
            }

            flag = property.FindPropertyRelative("keyData") is { isArray: true }
                && property.FindPropertyRelative("valueData") is { isArray: true };
            DictionaryFlags[key] = flag;

            return flag;
        }

        // Structure of a property path never changes for a given object, so the dictionary check is cached
        private static readonly Dictionary<(int Object, string Path), bool> DictionaryFlags = new();

        private static float GetDictionaryHeight(SerializedProperty property)
        {
            EnsureExpanded(property);

            var height = HeaderHeight + HeaderPadding * 2f;

            if (!property.isExpanded)
            {
                return height + BottomPadding + TopPadding;
            }

            var keys = property.FindPropertyRelative("keyData");
            var values = property.FindPropertyRelative("valueData");

            height += InnerPadding;

            var indices = GetVisibleIndices(property, keys);

            GetPageRange(property, indices.Count, out var start, out var end, out _, out _);

            for (var idx = start; idx < end; idx++)
            {
                height += GetPairHeight(keys, values, indices[idx]) + Spacing + ItemPadding * 2f;
            }

            return height + BottomPadding + TopPadding;
        }

        private static readonly Dictionary<(int Object, string Path), string> DictionarySearch = new();
        private static readonly Dictionary<(int Object, string Path), List<int>> IndicesCache = new();
        private static GUIStyle _pageButtonStyle;
        private static GUIStyle _placeholderStyle;

        private static string GetSearch(SerializedProperty property)
        {
            return DictionarySearch.TryGetValue((property.serializedObject.GetHashCode(), property.propertyPath), out var text) ? text : "";
        }

        private static void SetSearch(SerializedProperty property, string text)
        {
            DictionarySearch[(property.serializedObject.GetHashCode(), property.propertyPath)] = text ?? "";
            DictionaryPages[(property.serializedObject.GetHashCode(), property.propertyPath)] = 0;
            ClearHeightCache();
            GUI.changed = true;
        }

        private static string GetKeyText(SerializedProperty key)
        {
            switch (key.propertyType)
            {
                case SerializedPropertyType.Enum:
                    return key.enumValueIndex >= 0 && key.enumValueIndex < key.enumDisplayNames.Length ? key.enumDisplayNames[key.enumValueIndex] : "";
                case SerializedPropertyType.String:
                    return key.stringValue;
                case SerializedPropertyType.Integer:
                    return key.intValue.ToString();
                case SerializedPropertyType.Float:
                    return key.floatValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return key.objectReferenceValue != null ? key.objectReferenceValue.name : "";
                default:
                    return key.displayName;
            }
        }

        // Absolute indices of the entries matching the current search (all entries when the search is empty)
        private static List<int> GetVisibleIndices(SerializedProperty property, SerializedProperty keys)
        {
            var cacheKey = (property.serializedObject.GetHashCode(), property.propertyPath);

            if (IndicesCache.TryGetValue(cacheKey, out var cached) && cached.Count > 0 && cached[0] == -2 - keys.arraySize)
            {
                return cached.GetRange(1, cached.Count - 1);
            }

            var search = GetSearch(property);
            var result = new List<int> { -2 - keys.arraySize };

            for (var i = 0; i < keys.arraySize; i++)
            {
                if (search.Length == 0 || GetKeyText(keys.GetArrayElementAtIndex(i)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(i);
                }
            }

            IndicesCache[cacheKey] = result;

            return result.GetRange(1, result.Count - 1);
        }

        private static readonly Dictionary<(int Object, string Path), int> DictionaryPages = new();

        // Returns the visible index range of the current page, clamping the stored page to the item count
        private static void GetPageRange(SerializedProperty property, int count, out int start, out int end, out int page, out int pageCount)
        {
            var pageSize = PropertyUtility.GetAttribute<DictionaryDrawerSettingsAttribute>(property)?.PageSize ?? 0;

            if (pageSize <= 0)
            {
                start = 0;
                end = count;
                page = 0;
                pageCount = 1;

                return;
            }

            var key = (property.serializedObject.GetHashCode(), property.propertyPath);

            pageCount = Mathf.Max(1, Mathf.CeilToInt(count / (float) pageSize));
            DictionaryPages.TryGetValue(key, out page);
            page = Mathf.Clamp(page, 0, pageCount - 1);
            start = page * pageSize;
            end = Mathf.Min(count, start + pageSize);
        }

        private static void SetPage(SerializedProperty property, int page)
        {
            DictionaryPages[(property.serializedObject.GetHashCode(), property.propertyPath)] = page;
            ClearHeightCache();
            GUI.changed = true;
        }

        // Adds an entry with a key that is not used yet, otherwise the dictionary would collapse the duplicate
        private static void AddEntry(SerializedProperty keys, SerializedProperty values)
        {
            var count = keys.arraySize;

            keys.arraySize++;
            values.arraySize++;

            var newKey = keys.GetArrayElementAtIndex(count);

            if (newKey.propertyType == SerializedPropertyType.Enum)
            {
                var used = new HashSet<int>();

                for (var i = 0; i < count; i++)
                {
                    used.Add(keys.GetArrayElementAtIndex(i).enumValueIndex);
                }

                for (var i = 0; i < newKey.enumNames.Length; i++)
                {
                    if (!used.Contains(i))
                    {
                        newKey.enumValueIndex = i;
                        GUI.changed = true;

                        return;
                    }
                }

                keys.arraySize--;
                values.arraySize--;
            }
            else if (newKey.propertyType == SerializedPropertyType.Integer)
            {
                var max = int.MinValue;

                for (var i = 0; i < count; i++)
                {
                    max = Mathf.Max(max, keys.GetArrayElementAtIndex(i).intValue);
                }

                newKey.intValue = count == 0 ? 0 : max + 1;
            }
            else if (newKey.propertyType == SerializedPropertyType.String)
            {
                newKey.stringValue = "Key " + count;
            }
        }

        private static void ShowDictionaryContextMenu(SerializedProperty property, int index)
        {
            var serializedObject = property.serializedObject;
            var path = property.propertyPath;
            var menu = new GenericMenu();
            var clipboardKey = "dictionary:" + property.FindPropertyRelative("valueData").arrayElementType;

            void Apply(Action<SerializedProperty, SerializedProperty> action)
            {
                serializedObject.Update();

                var dictionary = serializedObject.FindProperty(path);

                action(dictionary.FindPropertyRelative("keyData"), dictionary.FindPropertyRelative("valueData"));
                serializedObject.ApplyModifiedProperties();
            }

            if (index >= 0)
            {
                var copied = CopyValue(property.FindPropertyRelative("valueData").GetArrayElementAtIndex(index));

                menu.AddItem(new GUIContent("Copy"), false, () =>
                {
                    _clipboard = copied;
                    _clipboardKey = clipboardKey;
                });

                if (_clipboardKey == clipboardKey)
                {
                    menu.AddItem(new GUIContent("Paste"), false, () => Apply((keys, values) => PasteValue(values.GetArrayElementAtIndex(index), _clipboard)));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste"));
                }

                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Delete"), false, () => Apply((keys, values) =>
                {
                    keys.DeleteArrayElementAtIndex(index);
                    values.DeleteArrayElementAtIndex(index);
                }));
            }

            menu.AddItem(new GUIContent("Clear"), false, () => Apply((keys, values) =>
            {
                keys.arraySize = 0;
                values.arraySize = 0;
            }));
            MyInspectorExtensions.InvokeCollectionContextMenu(menu, index >= 0
                ? property.FindPropertyRelative("valueData").GetArrayElementAtIndex(index).propertyPath
                : path);
            menu.ShowAsContext();
        }

        private static readonly Dictionary<(int Object, string Path), float> PairHeightCache = new();
        private static readonly Dictionary<(int Object, string Path), List<SerializedProperty>> ValueChildrenCache = new();

        private static float GetPairHeight(SerializedProperty keys, SerializedProperty values, int index)
        {
            var valueElement = values.GetArrayElementAtIndex(index);
            var cacheKey = (values.serializedObject.GetHashCode(), valueElement.propertyPath);

            if (PairHeightCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var key = EditorGUI.GetPropertyHeight(keys.GetArrayElementAtIndex(index), GUIContent.none, true);
            var value = GetDictionaryValueHeight(valueElement);
            var result = Mathf.Max(EditorGUIUtility.singleLineHeight, key, value);

            PairHeightCache[cacheKey] = result;

            return result;
        }

        private static List<SerializedProperty> GetValueChildren(SerializedProperty value)
        {
            var cacheKey = (value.serializedObject.GetHashCode(), value.propertyPath);

            if (ValueChildrenCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var result = ComputeValueChildren(value);

            ValueChildrenCache[cacheKey] = result;

            return result;
        }

        // Children of a [HideInInspector] list are flagged hidden, so NextVisible skips them: iterate with Next instead.
        private static List<SerializedProperty> ComputeValueChildren(SerializedProperty value)
        {
            var result = new List<SerializedProperty>();

            if (value.propertyType != SerializedPropertyType.Generic || value.isArray)
            {
                return result;
            }

            var type = value.boxedValue?.GetType();

            if (type == null || HasTypeDrawer(type) || type.Namespace != null && (type.Namespace.StartsWith("UnityEngine") || type.Namespace.StartsWith("System")))
            {
                return result;
            }

            var iterator = value.Copy();
            var end = iterator.GetEndProperty();

            if (!iterator.Next(true))
            {
                return result;
            }

            do
            {
                if (iterator.depth <= value.depth || SerializedProperty.EqualContents(iterator, end))
                {
                    break;
                }

                result.Add(iterator.Copy());
            }
            while (iterator.Next(false));

            return result;
        }

        private static float GetDictionaryValueHeight(SerializedProperty value)
        {
            var children = GetValueChildren(value);

            if (children.Count == 0)
            {
                return GetElementHeight(value, true);
            }

            var height = 0f;

            foreach (var run in GetRuns(value))
            {
                if (run.Foldout != null && IsBoxedContainerRun(run))
                {
                    height += GetContainerHeight(run.Children[0], false, false) + EditorGUIUtility.standardVerticalSpacing;

                    continue;
                }

                if (run.Foldout != null)
                {
                    height += GetFoldoutRunHeight(value, run.Foldout, run.Children);

                    continue;
                }

                foreach (var child in run.Children)
                {
                    height += GetElementHeight(child, false) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return Mathf.Max(0f, height - EditorGUIUtility.standardVerticalSpacing) + GetLooseButtonsHeight(value);
        }

        // A single container wrapped in a same-named Foldout is drawn as a regular boxed container, like its nested ones
        private static bool IsBoxedContainerRun((FoldoutAttribute Foldout, List<SerializedProperty> Children) run)
        {
            return run.Foldout != null && !run.Foldout.Clean && run.Children.Count == 1 && IsContainer(run.Children[0])
                && string.Equals(run.Foldout.Name, run.Children[0].displayName, StringComparison.OrdinalIgnoreCase);
        }

        private static void DrawDictionaryValue(Rect rect, SerializedProperty value)
        {
            var children = GetValueChildren(value);

            if (children.Count == 0)
            {
                Field(rect, value, GUIContent.none, true, true);

                return;
            }

            var y = rect.y;

            foreach (var run in GetRuns(value))
            {
                if (run.Foldout != null && IsBoxedContainerRun(run))
                {
                    var container = run.Children[0];
                    var containerHeight = GetContainerHeight(container, false, false);

                    DrawContainer(new Rect(rect.x, y, rect.width, containerHeight), container, null, false, false);
                    y += containerHeight + EditorGUIUtility.standardVerticalSpacing;

                    continue;
                }

                if (run.Foldout != null)
                {
                    DrawFoldoutRun(rect, value, run.Foldout, run.Children, ref y);

                    continue;
                }

                var previousOverride = _labelWidthOverride;

                _labelWidthOverride = GetWidestLabel(run.Children);

                foreach (var child in run.Children)
                {
                    var childHeight = GetElementHeight(child, false);

                    Field(new Rect(rect.x, y, rect.width, childHeight), child, null, false, true);
                    y += childHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                _labelWidthOverride = previousOverride;
            }

            DrawLooseButtons(rect, value, ref y);
        }

        private static readonly System.Reflection.PropertyInfo VisibleRectProperty =
            typeof(GUI).Assembly.GetType("UnityEngine.GUIClip")?.GetProperty("visibleRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private static Rect GetVisibleRect()
        {
            if (VisibleRectProperty == null)
            {
                return new Rect(-100000f, -100000f, 200000f, 200000f);
            }

            var rect = (Rect) VisibleRectProperty.GetValue(null);

            rect.yMin -= 60f;
            rect.yMax += 60f;

            return rect;
        }

        private static GUIStyle _countStyle;

        private const float CountWidth = InspectorStyle.ListCountWidth;
        private const float SearchWidth = InspectorStyle.ListSearchWidth;
        private const float MinColumnWidth = InspectorStyle.DictionaryMinColumnWidth;

        private static float GetKeyWidth(SerializedProperty property, float contentWidth)
        {
            var settings = PropertyUtility.GetAttribute<DictionaryDrawerSettingsAttribute>(property);
            var setting = settings?.KeyWidth ?? 125f;
            var width = setting <= 1f ? contentWidth * Mathf.Max(0f, setting) : setting;

            return Mathf.Clamp(width, MinColumnWidth, Mathf.Max(MinColumnWidth, contentWidth - MinColumnWidth));
        }

        private static void DrawDictionary(Rect rect, SerializedProperty property)
        {
            EnsureExpanded(property);

            rect.height = GetHeight(property) - TopPadding;

            var keys = property.FindPropertyRelative("keyData");
            var values = property.FindPropertyRelative("valueData");

            if (values.arraySize != keys.arraySize)
            {
                values.arraySize = keys.arraySize;
            }

            var count = keys.arraySize;
            var indices = GetVisibleIndices(property, keys);

            GetPageRange(property, indices.Count, out var pageStart, out var pageEnd, out var page, out var pageCount);

            var paged = (PropertyUtility.GetAttribute<DictionaryDrawerSettingsAttribute>(property)?.PageSize ?? 0) > 0;
            var headerBackground = new Rect(rect.x, rect.y, rect.width, HeaderHeight + HeaderPadding * 2f);
            var header = new Rect(rect.x, rect.y + HeaderPadding, rect.width, HeaderHeight);
            var addRect = new Rect(header.xMax - ButtonWidth, header.y + (header.height - 18f) * 0.5f, ButtonWidth, 18f);
            var nextRect = new Rect(addRect.x - ButtonWidth, addRect.y, ButtonWidth, 18f);
            var prevRect = new Rect(nextRect.x - ButtonWidth, addRect.y, ButtonWidth, 18f);
            var searchRight = paged ? prevRect.x - 2f : addRect.x - 2f;
            var searchRect = new Rect(searchRight - SearchWidth, addRect.y + 1f, SearchWidth, 16f);
            var countRect = new Rect(searchRect.x - CountWidth - 4f, header.y, CountWidth, header.height);
            var labelRect = new Rect(header.x + 18f, header.y, countRect.x - header.x - 18f, header.height);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(headerBackground, InspectorStyle.HeaderBackground);
                DrawBorder(rect);
            }

            if (Event.current.type == EventType.ContextClick && headerBackground.Contains(Event.current.mousePosition))
            {
                ShowDictionaryContextMenu(property, -1);
                Event.current.Use();

                return;
            }

            property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, PropertyUtility.GetLabel(property), true);

            _countStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontSize = 12 };

            var countStyle = _countStyle;

            countStyle.normal.textColor = count == 0 ? InspectorStyle.CountLabel : EditorStyles.miniLabel.normal.textColor;

            EditorGUI.LabelField(countRect, count == 0 ? "Empty" : (count == 1 ? "1 Item" : count + " Items"), countStyle);

            _pageButtonStyle ??= new GUIStyle(EditorStyles.iconButton)
            {
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 2), fontStyle = FontStyle.Bold
            };
            _placeholderStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 0, 0, 0) };

            var searchName = "DictSearch_" + property.propertyPath;
            var search = GetSearch(property);

            GUI.SetNextControlName(searchName);

            var newSearch = GUI.TextField(searchRect, search, EditorStyles.toolbarSearchField);

            if (search.Length == 0 && GUI.GetNameOfFocusedControl() != searchName && Event.current.type == EventType.Repaint)
            {
                var placeholderColor = _placeholderStyle.normal.textColor;

                _placeholderStyle.normal.textColor = InspectorStyle.SearchPlaceholder;
                _placeholderStyle.Draw(searchRect, "Search..", false, false, false, false);
                _placeholderStyle.normal.textColor = placeholderColor;
            }

            if (newSearch != search)
            {
                SetSearch(property, newSearch);

                return;
            }

            if (paged)
            {
                var newPage = page;

                using (new EditorGUI.DisabledScope(page <= 0))
                {
                    if (GUI.Button(prevRect, new GUIContent("<", $"Page {page + 1}/{pageCount}"), _pageButtonStyle))
                    {
                        newPage = page - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(page >= pageCount - 1))
                {
                    if (GUI.Button(nextRect, new GUIContent(">", $"Page {page + 1}/{pageCount}"), _pageButtonStyle))
                    {
                        newPage = page + 1;
                    }
                }

                if (newPage != page)
                {
                    SetPage(property, newPage);

                    return;
                }
            }

            if (GUI.Button(addRect, EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.iconButton))
            {
                AddEntry(keys, values);
                ExpandForAdd(property);

                if (search.Length > 0)
                {
                    SetSearch(property, "");
                }

                if (paged)
                {
                    SetPage(property, Mathf.Max(0, (keys.arraySize - 1) / PropertyUtility.GetAttribute<DictionaryDrawerSettingsAttribute>(property).PageSize));
                }

                return;
            }

            if (!property.isExpanded)
            {
                return;
            }

            var y = headerBackground.yMax + InnerPadding;
            var visible = GetVisibleRect();
            var eventType = Event.current.type;
            var keyWidth = GetKeyWidth(property, rect.width - 8f - ButtonWidth - 8f);

            // Skip off-screen rows, except while dragging (hot control), so control ids stay consistent.
            var cull = (eventType == EventType.Repaint || eventType == EventType.Layout
                        || eventType == EventType.ScrollWheel || eventType == EventType.MouseMove
                        || eventType == EventType.Used || eventType == EventType.Ignore)
                       && GUIUtility.hotControl == 0;

            for (var idx = pageStart; idx < pageEnd; idx++)
            {
                var i = indices[idx];
                var height = GetPairHeight(keys, values, i);
                var rowRect = new Rect(rect.x + 1f, y - Spacing * 0.5f, rect.width - 2f, height + Spacing + ItemPadding * 2f);
                var contentX = rect.x + 8f;
                var contentWidth = rect.width - 8f - ButtonWidth - 8f;
                var keyRect = new Rect(contentX, y + ItemPadding, keyWidth - 2f, height);
                var valueRect = new Rect(contentX + keyWidth + 2f, y + ItemPadding, contentWidth - keyWidth - 2f, height);
                var removeRect = new Rect(rect.xMax - ButtonWidth - 2f, y + ItemPadding + (height - 20f) * 0.5f, ButtonWidth, 20f);

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rowRect, idx % 2 == 0 ? EvenRowColor : OddRowColor);
                }

                if (cull && (rowRect.yMax < visible.yMin || rowRect.yMin > visible.yMax))
                {
                    y += height + Spacing + ItemPadding * 2f;

                    continue;
                }

                var labelWidth = EditorGUIUtility.labelWidth;

                EditorGUIUtility.labelWidth = Mathf.Min(labelWidth, 80f);
                EditorGUI.PropertyField(keyRect, keys.GetArrayElementAtIndex(i), GUIContent.none, true);
                DrawDictionaryValue(valueRect, values.GetArrayElementAtIndex(i));
                EditorGUIUtility.labelWidth = labelWidth;

                // Handled after the content so the innermost row consumes the click first
                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    ShowDictionaryContextMenu(property, i);
                    Event.current.Use();

                    return;
                }

                if (GUI.Button(removeRect, "x", RemoveStyle))
                {
                    keys.DeleteArrayElementAtIndex(i);
                    values.DeleteArrayElementAtIndex(i);

                    return;
                }

                y += height + Spacing + ItemPadding * 2f;
            }
        }

        private static readonly Dictionary<(int Object, string Path, int Flags), float> HeightCache = new();

        /// <summary>
        /// Clears the per-event height cache. Must be called at the start of every inspector OnGUI.
        /// </summary>
        private static bool _invalidateCaches = true;
        private static int _lastTargetId;
        private static int _lastDirtyCount = -1;

        /// <summary>
        /// Clears the caches only when the layout may have changed (data edited, user interaction, play mode),
        /// so passive events like repaints, mouse moves and scrolling reuse the computed heights.
        /// </summary>
        public static void BeginInspector(UnityEngine.Object target)
        {
            var type = Event.current.type;
            var targetId = target != null ? target.GetHashCode() : 0;
            var dirtyCount = target != null ? EditorUtility.GetDirtyCount(target) : 0;
            // While a control is being dragged or a text/number field is being typed in, value edits do not change the layout: defer the invalidation until release or validation
            var dragging = (GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField) && targetId == _lastTargetId;
            var dirtyChanged = dirtyCount != _lastDirtyCount && !dragging;
            var changed = _invalidateCaches || targetId != _lastTargetId || dirtyChanged
                          || EditorApplication.isPlaying && type == EventType.Layout && !dragging;

            _lastTargetId = targetId;

            if (!dragging)
            {
                _lastDirtyCount = dirtyCount;
            }
            _invalidateCaches = false;

            if (changed)
            {
                ClearHeightCache();
            }

            switch (type)
            {
                case EventType.Layout:
                case EventType.Repaint:
                case EventType.MouseMove:
                case EventType.ScrollWheel:
                case EventType.MouseDrag:
                case EventType.KeyUp:
                case EventType.KeyDown:
                case EventType.Used:
                case EventType.Ignore:
                case EventType.MouseEnterWindow:
                case EventType.MouseLeaveWindow:
                    break;
                default:
                    _invalidateCaches = true;

                    break;
            }
        }

        /// <summary>Incremented each time the height caches are cleared; lets external drawers invalidate their own caches.</summary>
        public static int CacheVersion { get; private set; }

        public static void ClearHeightCache()
        {
            CacheVersion++;
            HeightCache.Clear();
            RunsCache.Clear();
            RunParents.Clear();
            TabLayoutCache.Clear();
            BodyCache.Clear();
            PairHeightCache.Clear();
            IndicesCache.Clear();
            DictionaryFlags.Clear();
            ContainerFlags.Clear();
            ValueChildrenCache.Clear();
            PropertyUtility.ClearVisibilityCache();
        }

        private static readonly Dictionary<(int Object, string Path, int Flags), List<(FoldoutAttribute Foldout, List<SerializedProperty> Children)>> RunsCache = new();

        public static float GetHeight(SerializedProperty property, bool flat = false, bool plain = false)
        {
            var key = (property.serializedObject.GetHashCode(), property.propertyPath, (flat ? 1 : 0) | (plain ? 2 : 0) | (PropertyUtility.ForceVisible ? 4 : 0));

            if (HeightCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var result = ComputeHeight(property, flat, plain);

            HeightCache[key] = result;

            return result;
        }

        // Rows holding a GUILayout-based table must not be culled, otherwise Layout and Repaint disagree on the control count
        private static bool ContainsTable(Type type, int depth)
        {
            if (type == null || depth > 3 || type.IsPrimitive || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            if (TableTypeCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            TableTypeCache[type] = false;

            var result = false;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetCustomAttribute<TableListAttribute>() != null)
                {
                    result = true;

                    break;
                }

                var fieldType = field.FieldType;

                if (fieldType.IsArray)
                {
                    fieldType = fieldType.GetElementType();
                }
                else if (fieldType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(fieldType))
                {
                    fieldType = fieldType.GetGenericArguments()[0];
                }

                if (ContainsTable(fieldType, depth + 1))
                {
                    result = true;

                    break;
                }
            }

            TableTypeCache[type] = result;

            return result;
        }

        private static readonly Dictionary<Type, bool> TableTypeCache = new Dictionary<Type, bool>();
        private static readonly Dictionary<(int, string), Rect> TableAreaRects = new Dictionary<(int, string), Rect>();

        private static bool TryGetTable(SerializedProperty property, out System.Collections.IList list, out TableListAttribute attribute)
        {
            list = null;
            attribute = null;

            if (!property.isArray || property.propertyType == SerializedPropertyType.String || IsDictionary(property))
            {
                return false;
            }

            attribute = PropertyUtility.GetAttribute<TableListAttribute>(property);

            if (attribute == null)
            {
                return false;
            }

            list = PropertyUtility.GetTargetObjectOfProperty(property) as System.Collections.IList;

            return list != null;
        }

        private static float ComputeHeight(SerializedProperty property, bool flat, bool plain)
        {
            if (TryGetTable(property, out _, out _))
            {
                return MyTableDrawer.GetHeight(property);
            }

            if (IsDictionary(property))
            {
                return GetDictionaryHeight(property);
            }

            if (IsNullReference(property))
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (IsContainer(property))
            {
                return GetContainerHeight(property, flat || IsInline(property), plain);
            }

            if (!IsList(property))
            {
                var propertyHeight = EditorGUI.GetPropertyHeight(property, true);

                return IsUnityEvent(property) ? propertyHeight + InspectorStyle.UnityEventTopMargin : propertyHeight;
            }

            EnsureExpanded(property);

            var settings = GetSettings(property);
            var height = GetListBodyHeight(property, settings);

            if (!plain && (!IsExpanded(property, settings) || property.arraySize == 0))
            {
                height += BottomPadding;
            }

            return plain ? height : height + TopPadding;
        }

        private static float GetListBodyHeight(SerializedProperty property, ListDrawerSettingsAttribute settings)
        {
            var height = HeaderHeight + HeaderPadding * 2f;

            if (!IsExpanded(property, settings) || property.arraySize == 0)
            {
                return height;
            }

            height += Spacing * 0.5f;

            for (var i = 0; i < property.arraySize; i++)
            {
                height += GetElementHeight(property.GetArrayElementAtIndex(i),                 settings == null || !settings.ShowFoldout) + Spacing + ItemPadding * 2f;
            }

            return height + BottomPadding;
        }

        public static void Field(Rect rect, SerializedProperty property, GUIContent label = null, bool flat = false, bool plain = false)
        {
            if (TryGetTable(property, out var tableList, out var tableAttribute))
            {
                // The area keeps the rect given during Layout, where rect-based rows have no valid position yet: reuse the last real one
                var areaKey = (property.serializedObject.targetObject != null ? property.serializedObject.targetObject.GetHashCode() : 0, property.propertyPath);

                if (Event.current.type == EventType.Layout)
                {
                    rect = TableAreaRects.TryGetValue(areaKey, out var stored)
                        ? new Rect(stored.x, stored.y, stored.width, rect.height)
                        : new Rect(rect.x, rect.y, EditorGUIUtility.currentViewWidth - 40f, MyTableDrawer.GetHeight(property));
                }
                else if (rect.width > 1f)
                {
                    TableAreaRects[areaKey] = rect;
                }

                // Flush pending edits of previously drawn fields, otherwise the Update below would discard them
                property.serializedObject.ApplyModifiedProperties();

                GUILayout.BeginArea(rect);
                GUILayout.BeginVertical(GUILayout.ExpandHeight(false));
                MyTableDrawer.Draw(tableList, property.serializedObject.targetObject, ObjectNames.NicifyVariableName(property.name),
                    tableAttribute.IsReadOnly, null, property);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndArea();
                property.serializedObject.Update();
            }
            else if (IsDictionary(property))
            {
                DrawDictionary(rect, property);
            }
            else if (IsList(property))
            {
                Draw(rect, property);
            }
            else if (IsNullReference(property))
            {
                DrawNullReferenceButton(rect, property, label);
            }
            else if (IsContainer(property))
            {
                var inline = !flat && IsInline(property);

                if (inline)
                {
                    rect = new Rect(rect.x + ContainerIndent, rect.y, rect.width - ContainerIndent * 2f, rect.height);
                }

                DrawContainer(rect, property, label, flat || inline, plain);
            }
            else
            {
                // Plain fields never react to these events, so skipping them saves most of the cost of wheel scrolling
                var eventType = Event.current.type;

                if (eventType == EventType.ScrollWheel || eventType == EventType.MouseMove)
                {
                    return;
                }

                var previousLabelWidth = EditorGUIUtility.labelWidth;

                EditorGUIUtility.labelWidth = _labelWidthOverride > 0f
                    ? Mathf.Min(_labelWidthOverride, rect.width * 0.6f)
                    : Mathf.Min(previousLabelWidth, Mathf.Max(80f, rect.width * 0.3f));

                if (label == null && PropertyUtility.GetAttribute<LabelAttribute>(property) != null)
                {
                    label = PropertyUtility.GetLabel(property);
                }

                if (IsUnityEvent(property))
                {
                    rect = new Rect(rect.x - InspectorStyle.UnityEventLeftIndent, rect.y + InspectorStyle.UnityEventTopMargin, rect.width + InspectorStyle.UnityEventLeftIndent, rect.height - InspectorStyle.UnityEventTopMargin);
                }

                if (label != null)
                {
                    EditorGUI.PropertyField(rect, property, label, true);
                }
                else
                {
                    EditorGUI.PropertyField(rect, property, true);
                }

                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        // A null [SerializeReference] field has no children to draw: offer a button to create the instance
        private static void DrawNullReferenceButton(Rect rect, SerializedProperty property, GUIContent label)
        {
            // The field type name given by Unity is reliable even when the path crosses derived managed references
            var baseType = ResolveManagedReferenceType(property.managedReferenceFieldTypename) ?? ResolveDeclaredType(property);
            var buttonRect = EditorGUI.PrefixLabel(rect, label ?? new GUIContent(property.displayName));
            var singleType = GetSingleType(baseType);
            var canInstantiate = singleType != null;

            if (!EditorGUI.DropdownButton(buttonRect, new GUIContent(canInstantiate ? "Initialize" : "Select..."), FocusType.Keyboard, canInstantiate ? EditorStyles.miniButton : EditorStyles.popup))
            {
                return;
            }

            var serializedObject = property.serializedObject;
            var path = property.propertyPath;

            void Assign(Type type)
            {
                serializedObject.Update();
                serializedObject.FindProperty(path).managedReferenceValue = Activator.CreateInstance(type);
                serializedObject.ApplyModifiedProperties();
                ClearHeightCache();
            }

            if (canInstantiate)
            {
                Assign(singleType);
            }
            else if (baseType != null)
            {
                ShowTypeMenu(baseType, Assign);
            }
        }

        private static readonly Dictionary<Type, Type> SingleTypeCache = new Dictionary<Type, Type>();

        // Returns the only instantiable type for the base type, or null when there is none or several choices
        private static Type GetSingleType(Type baseType)
        {
            if (baseType == null)
            {
                return null;
            }

            if (SingleTypeCache.TryGetValue(baseType, out var cached))
            {
                return cached;
            }

            var types = GetTypes(baseType);
            var result = types.Count == 1 && types[0].GetConstructor(Type.EmptyTypes) != null ? types[0] : null;

            SingleTypeCache[baseType] = result;

            return result;
        }

        private static bool IsInline(SerializedProperty property)
        {
            // A field wrapped in a Foldout of the same name would repeat its header, so it is drawn flat
            var foldout = PropertyUtility.GetAttribute<FoldoutAttribute>(property);

            return PropertyUtility.GetAttribute<InlinePropertyAttribute>(property) != null
                || PropertyUtility.GetAttribute<HideLabelAttribute>(property) != null
                || foldout != null && string.Equals(foldout.Name, property.displayName, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly Dictionary<(int, string), bool> UnityEventFlags = new();

        private static bool IsUnityEvent(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.Generic || property.isArray)
            {
                return false;
            }

            var key = (property.serializedObject.GetHashCode(), property.propertyPath);

            if (UnityEventFlags.TryGetValue(key, out var flag))
            {
                return flag;
            }

            var type = ResolveDeclaredType(property);

            flag = property.type.StartsWith("UnityEvent", StringComparison.Ordinal)
                   || (type != null && typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(type));
            UnityEventFlags[key] = flag;

            return flag;
        }

        /// <summary>
        /// True for serializable classes/structs without a custom drawer. They are drawn manually so that the lists they contain use this drawer.
        /// </summary>
        public static bool IsContainer(SerializedProperty property)
        {
            // Children of a [HideInInspector] field (like dictionary values) are flagged hidden, so hasVisibleChildren is false for them
            if (property.isArray || !property.hasChildren)
            {
                return false;
            }

            Type type;

            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                type = ResolveManagedReferenceType(property.managedReferenceFullTypename) ?? property.managedReferenceValue?.GetType();
            }
            else if (property.propertyType == SerializedPropertyType.Generic)
            {
                var key = (property.serializedObject.GetHashCode(), property.propertyPath);

                if (ContainerFlags.TryGetValue(key, out var cachedFlag))
                {
                    return cachedFlag;
                }

                type = ResolveDeclaredType(property);

                if (type == null)
                {
                    try
                    {
                        type = property.boxedValue?.GetType();
                    }
                    catch (NotSupportedException)
                    {
                        // Built-in types such as UnityEvent cannot be read with boxedValue
                    }
                }

                cachedFlag = IsContainerType(type);
                ContainerFlags[key] = cachedFlag;

                return cachedFlag;
            }
            else
            {
                return false;
            }

            return IsContainerType(type);
        }

        // Generic fields have a fixed declared type, so their container status is cached until the next cache clear
        private static readonly Dictionary<(int Object, string Path), bool> ContainerFlags = new();

        private static bool IsContainerType(Type type)
        {
            if (type == null || type.Namespace != null && (type.Namespace.StartsWith("UnityEngine") || type.Namespace.StartsWith("System")))
            {
                return false;
            }

            return !HasTypeDrawer(type);
        }

        private static readonly Dictionary<string, Type> ManagedTypeCache = new Dictionary<string, Type>();
        private static readonly Dictionary<(Type, string), Type> DeclaredTypeCache = new Dictionary<(Type, string), Type>();

        private static Type ResolveManagedReferenceType(string fullTypename)
        {
            if (string.IsNullOrEmpty(fullTypename))
            {
                return null;
            }

            if (ManagedTypeCache.TryGetValue(fullTypename, out var cached))
            {
                return cached;
            }

            Type result = null;
            var separator = fullTypename.IndexOf(' ');

            if (separator > 0)
            {
                var assemblyName = fullTypename.Substring(0, separator);
                var typeName = fullTypename.Substring(separator + 1).Replace('/', '+');

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == assemblyName)
                    {
                        result = assembly.GetType(typeName);

                        break;
                    }
                }
            }

            ManagedTypeCache[fullTypename] = result;

            return result;
        }

        /// <summary>
        /// Resolves the declared type of a property by walking its path with reflection, which avoids copying the whole value with boxedValue.
        /// Returns null when the path cannot be resolved.
        /// </summary>
        private static Type ResolveDeclaredType(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;

            if (target == null)
            {
                return null;
            }

            var path = System.Text.RegularExpressions.Regex.Replace(property.propertyPath, @"\[\d+\]", "[]");
            var key = (target.GetType(), path);

            if (DeclaredTypeCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var current = target.GetType();
            var parts = path.Split('.');

            for (var i = 0; i < parts.Length && current != null; i++)
            {
                if (parts[i] == "Array" && i + 1 < parts.Length && parts[i + 1] == "data[]")
                {
                    current = current.IsArray ? current.GetElementType() : current.IsGenericType ? current.GetGenericArguments()[0] : null;
                    i++;

                    continue;
                }

                FieldInfo field = null;

                for (var type = current; type != null && field == null; type = type.BaseType)
                {
                    field = type.GetField(parts[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                }

                current = field?.FieldType;
            }

            DeclaredTypeCache[key] = current;

            return current;
        }

        private static List<(Type Type, bool UseForChildren)> _typeDrawers;
        private static readonly Dictionary<Type, bool> TypeDrawerCache = new Dictionary<Type, bool>();

        private static bool HasTypeDrawer(Type type)
        {
            if (TypeDrawerCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            if (_typeDrawers == null)
            {
                _typeDrawers = new List<(Type, bool)>();

                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var typeField = typeof(CustomPropertyDrawer).GetField("m_Type", flags);
                var childrenField = typeof(CustomPropertyDrawer).GetField("m_UseForChildren", flags);

                foreach (var drawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
                {
                    foreach (var attribute in drawerType.GetCustomAttributes<CustomPropertyDrawer>(true))
                    {
                        if (typeField?.GetValue(attribute) is Type target)
                        {
                            _typeDrawers.Add((target, childrenField != null && (bool) childrenField.GetValue(attribute)));
                        }
                    }
                }
            }

            var result = _typeDrawers.Any(d => d.Type == type || d.UseForChildren && d.Type.IsAssignableFrom(type));

            TypeDrawerCache[type] = result;

            return result;
        }

        private static IEnumerable<SerializedProperty> GetChildren(SerializedProperty property)
        {
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            // Under a hidden ancestor NextVisible skips everything, so iterate with Next instead
            var underHidden = !property.hasVisibleChildren;

            if (!(underHidden ? iterator.Next(true) : iterator.NextVisible(true)))
            {
                yield break;
            }

            do
            {
                if (SerializedProperty.EqualContents(iterator, end) || iterator.depth <= property.depth)
                {
                    yield break;
                }

                if (PropertyUtility.IsVisible(iterator))
                {
                    yield return iterator.Copy();
                }
            }
            while (underHidden ? iterator.Next(false) : iterator.NextVisible(false));
        }

        private static readonly Dictionary<Type, MemberInfo> FoldoutLabelMembers = new();

        private static MemberInfo GetFoldoutLabelMember(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (FoldoutLabelMembers.TryGetValue(type, out var cached))
            {
                return cached;
            }

            MemberInfo member = null;
            var attribute = type.GetCustomAttribute<FoldoutLabelAttribute>(true);

            if (attribute != null)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                member = (MemberInfo) type.GetMethod(attribute.MemberName, flags, null, Type.EmptyTypes, null)
                    ?? type.GetProperty(attribute.MemberName, flags);
            }

            FoldoutLabelMembers[type] = member;

            return member;
        }

        /// <summary>Text given by the [FoldoutLabel] of the class of the property value, or null when there is none.</summary>
        private static string GetFoldoutLabelText(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference && property.propertyType != SerializedPropertyType.Generic)
            {
                return null;
            }

            var target = PropertyUtility.GetTargetObjectOfProperty(property);

            if (target == null)
            {
                return null;
            }

            var member = GetFoldoutLabelMember(target.GetType());

            if (member == null)
            {
                return null;
            }

            try
            {
                return (member is MethodInfo method ? method.Invoke(target, null) : ((PropertyInfo) member).GetValue(target)) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static float GetFoldoutLabelExtraHeight(SerializedProperty property)
        {
            var text = GetFoldoutLabelText(property);

            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            var extraLines = 0;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    extraLines++;
                }
            }

            return extraLines * EditorGUIUtility.singleLineHeight;
        }

        private static float GetContainerHeight(SerializedProperty property, bool flat, bool plain)
        {
            if (flat)
            {
                return GetRunsHeight(property);
            }

            var extra = GetFoldoutLabelExtraHeight(property);

            if (plain)
            {
                return property.isExpanded
                    ? extra + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + GetRunsHeight(property)
                    : extra + EditorGUIUtility.singleLineHeight;
            }

            var headerHeight = TopPadding + HeaderHeight + HeaderPadding * 2f + extra;

            if (!property.isExpanded)
            {
                return headerHeight + BottomPadding;
            }

            return headerHeight + BoxPadding * 2f + GetRunsHeight(property) + BottomPadding;
        }

        private static float GetRunsHeight(SerializedProperty property)
        {
            var height = 0f;

            foreach (var run in GetRuns(property))
            {
                if (run.Foldout == null)
                {
                    height += GetChildrenHeight(run.Children);

                    continue;
                }

                height += GetFoldoutRunHeight(property, run.Foldout, run.Children);
            }

            return height + GetLooseButtonsHeight(property);
        }

        private static readonly Dictionary<Type, List<System.Reflection.MethodInfo>> LooseButtonCache = new();

        // [Button] methods without a [Foldout] on the container type are drawn after all its fields
        private static List<System.Reflection.MethodInfo> GetLooseButtons(SerializedProperty property)
        {
            var target = PropertyUtility.GetTargetObjectOfProperty(property);

            if (target == null)
            {
                return null;
            }

            var type = target.GetType();

            if (LooseButtonCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var buttons = new List<System.Reflection.MethodInfo>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetCustomAttributes(typeof(ButtonAttribute), true).Length == 0
                    || method.GetCustomAttributes(typeof(FoldoutAttribute), true).Length > 0)
                {
                    continue;
                }

                var valid = true;

                foreach (var parameter in method.GetParameters())
                {
                    if (!parameter.IsOptional)
                    {
                        valid = false;

                        break;
                    }
                }

                if (valid)
                {
                    buttons.Add(method);
                }
            }

            LooseButtonCache[type] = buttons;

            return buttons;
        }

        private static float GetLooseButtonsHeight(SerializedProperty property)
        {
            var buttons = GetLooseButtons(property);

            return buttons == null ? 0f : buttons.Count * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        }

        private static void DrawLooseButtons(Rect rect, SerializedProperty property, ref float y)
        {
            var buttons = GetLooseButtons(property);

            if (buttons == null || buttons.Count == 0)
            {
                return;
            }

            var target = PropertyUtility.GetTargetObjectOfProperty(property);

            foreach (var method in buttons)
            {
                var attribute = (ButtonAttribute)method.GetCustomAttributes(typeof(ButtonAttribute), true)[0];
                var text = string.IsNullOrEmpty(attribute.Text) ? ObjectNames.NicifyVariableName(method.Name) : attribute.Text;

                if (GUI.Button(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), text))
                {
                    var serializedObject = property.serializedObject;
                    var parameters = method.GetParameters();
                    var arguments = new object[parameters.Length];

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        arguments[i] = parameters[i].DefaultValue;
                    }

                    serializedObject.ApplyModifiedProperties();
                    method.Invoke(target, arguments);
                    EditorUtility.SetDirty(serializedObject.targetObject);
                    serializedObject.Update();
                    GUIUtility.ExitGUI();
                }

                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        public static float GetFoldoutRunHeight(SerializedProperty property, FoldoutAttribute foldout, List<SerializedProperty> children)
        {
            var height = 0f;
            var toggle = GetToggle(children);
            var body = GetBody(property, foldout, children, toggle);

            if (foldout.Clean)
            {
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                if (IsFoldoutOpen(property, foldout.Name))
                {
                    height += GetBodyHeight(toggle, body) + GetButtonsHeight(property, foldout);
                }

                return height;
            }

            height += FoldoutTopSpace + BoxHeaderHeight;

            if (IsFoldoutOpen(property, foldout.Name))
            {
                height += BoxPadding * 2f + GetBodyHeight(toggle, body) + GetButtonsHeight(property, foldout);
            }

            height += FoldoutBottomSpace;

            return height;
        }

        private static readonly Dictionary<(Type Type, string Foldout), List<System.Reflection.MethodInfo>> ButtonCache = new();

        // [Button] methods tagged with the same [Foldout] are drawn at the end of that foldout
        private static List<System.Reflection.MethodInfo> GetButtons(SerializedProperty property, FoldoutAttribute foldout)
        {
            var target = PropertyUtility.GetTargetObjectOfProperty(property);

            if (target == null)
            {
                return null;
            }

            var key = (target.GetType(), foldout.Name);

            if (ButtonCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var buttons = target.GetType().GetMethods(flags)
                .Where(m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0
                    && m.GetParameters().All(p => p.IsOptional)
                    && m.GetCustomAttributes(typeof(FoldoutAttribute), true).Cast<FoldoutAttribute>().Any(f => f.Name == foldout.Name))
                .ToList();

            ButtonCache[key] = buttons;

            return buttons;
        }

        private static float GetButtonsHeight(SerializedProperty property, FoldoutAttribute foldout)
        {
            var buttons = GetButtons(property, foldout);

            return buttons == null ? 0f : buttons.Count * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        }

        private static void DrawButtons(Rect rect, SerializedProperty property, FoldoutAttribute foldout, ref float y)
        {
            var buttons = GetButtons(property, foldout);

            if (buttons == null)
            {
                return;
            }

            var target = PropertyUtility.GetTargetObjectOfProperty(property);

            foreach (var method in buttons)
            {
                var attribute = (ButtonAttribute)method.GetCustomAttributes(typeof(ButtonAttribute), true)[0];
                var text = string.IsNullOrEmpty(attribute.Text) ? ObjectNames.NicifyVariableName(method.Name) : attribute.Text;

                if (GUI.Button(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), text))
                {
                    method.Invoke(target, method.GetParameters().Select(p => p.DefaultValue).ToArray());
                }

                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private const float BoxHeaderHeight = HeaderHeight + HeaderPadding * 2f;
        private const float BoxPadding = InspectorStyle.ListBoxPadding;
        private const float FoldoutTopSpace = InspectorStyle.FoldoutTopSpace;
        private const float FoldoutBottomSpace = InspectorStyle.FoldoutBottomSpace;
        private const float ContainerIndent = InspectorStyle.ListContainerIndent;
        private const float BodyIndentLeft = InspectorStyle.FoldoutBodyIndentLeft;
        private const float BodyIndentRight = InspectorStyle.FoldoutBodyIndentRight;

        private static SerializedProperty GetToggle(List<SerializedProperty> children)
        {
            var first = children[0];

            return first.propertyType == SerializedPropertyType.Boolean && PropertyUtility.GetAttribute<BoolTitleAttribute>(first) != null ? first : null;
        }

        private static readonly Dictionary<(int Object, string Path, string Foldout, int Flags), List<SerializedProperty>> BodyCache = new();

        // Bodies of foldouts: their fields already belong to the tab drawn around the foldout, so no toolbar is drawn inside
        private static readonly HashSet<List<SerializedProperty>> FoldoutBodies =         new();

        private static List<SerializedProperty> GetBody(SerializedProperty property, FoldoutAttribute foldout, List<SerializedProperty> children, SerializedProperty toggle)
        {
            if (toggle == null)
            {
                FoldoutBodies.Add(children);

                return children;
            }

            var key = (property.serializedObject.GetHashCode(), property.propertyPath, foldout.Name, (PropertyUtility.ForceVisible ? 1 : 0) | (toggle.boolValue ? 2 : 0));

            if (BodyCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var body = ComputeBody(property, foldout, children, toggle);

            BodyCache[key] = body;
            FoldoutBodies.Add(body);

            return body;
        }

        private static List<SerializedProperty> ComputeBody(SerializedProperty property, FoldoutAttribute foldout, List<SerializedProperty> children, SerializedProperty toggle)
        {
            if (toggle == null)
            {
                return children;
            }

            if (toggle.boolValue)
            {
                return children.GetRange(1, children.Count - 1);
            }

            var previous = PropertyUtility.ForceVisible;

            PropertyUtility.ForceVisible = true;

            var body = new List<SerializedProperty>();

            foreach (var c in GetChildren(property))
            {
                if (c.propertyPath != toggle.propertyPath && PropertyUtility.GetAttribute<FoldoutAttribute>(c)?.Name == foldout.Name)
                {
                    body.Add(c);
                }
            }

            PropertyUtility.ForceVisible = previous;

            return body;
        }

        private static float GetBodyHeight(SerializedProperty toggle, List<SerializedProperty> body)
        {
            var previous = PropertyUtility.ForceVisible;

            PropertyUtility.ForceVisible = previous || (toggle != null && !toggle.boolValue);

            var height = GetChildrenHeight(body);

            PropertyUtility.ForceVisible = previous;

            return height;
        }

        private static float GetChildrenHeight(List<SerializedProperty> children)
        {
            var height = 0f;
            var layout = GetTabLayout(children);

            if (layout.Count > 0)
            {
                for (var i = 0; i < layout.Count; i++)
                {
                    var entry = layout[i];

                    if (entry.Toolbar != null)
                    {
                        height += EditorGUIUtility.singleLineHeight;

                        continue;
                    }

                    if (entry.FoldoutChildren != null)
                    {
                        height += GetFoldoutRunHeight(entry.Parent, entry.Foldout, entry.FoldoutChildren);

                        continue;
                    }

                    height += GetTitleHeight(entry.Child) + GetHeight(entry.Child) + EditorGUIUtility.standardVerticalSpacing;
                    height += (entry.BoxStart ? TabGroupUtility.BoxPaddingVertical : 0f) + (entry.BoxEnd ? TabGroupUtility.BoxPaddingVertical : 0f);
                }

                return height;
            }

            foreach (var child in children)
            {
                height += GetTitleHeight(child) + GetHeight(child) + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        // One entry per drawn element of a children list that contains [Tab] fields: toolbars and only the fields of the selected tabs
        private class TabEntry
        {
            public SerializedProperty Child;
            public FoldoutAttribute Foldout;
            public List<SerializedProperty> FoldoutChildren;
            public SerializedProperty Parent;
            public TabGroupUtility.TabGroup Toolbar;
            public string[] ToolbarNames;
            public bool InBox;
            public bool BoxStart;
            public bool BoxEnd;
            public float LabelWidth;
        }

        private static readonly List<TabEntry> NoTabs = new();
        private static readonly Dictionary<List<SerializedProperty>, List<TabEntry>> TabLayoutCache = new();

        private static List<TabEntry> GetTabLayout(List<SerializedProperty> children)
        {
            if (TabLayoutCache.TryGetValue(children, out var cached))
            {
                return cached;
            }

            var layout = FoldoutBodies.Contains(children) ? NoTabs : ComputeTabLayout(children);

            TabLayoutCache[children] = layout;

            return layout;
        }

        private static List<TabEntry> ComputeTabLayout(List<SerializedProperty> children)
        {
            List<TabAttribute> tabs = null;
            string firstPath = null;

            foreach (var child in children)
            {
                var tab = PropertyUtility.GetAttribute<TabAttribute>(child);

                if (tab == null)
                {
                    continue;
                }

                tabs ??= new List<TabAttribute>();
                firstPath ??= child.propertyPath;
                tabs.Add(tab);
            }

            if (tabs == null)
            {
                return NoTabs;
            }

            var targetName = children[0].serializedObject.targetObject.name;
            var groups = TabGroupUtility.BuildGroups(tabs, key => $"MyListTab.{targetName}.{firstPath}.{key}");
            var emitted = new HashSet<string>();
            var layout = new List<TabEntry>();

            foreach (var child in children)
            {
                var tab = PropertyUtility.GetAttribute<TabAttribute>(child);

                if (tab == null)
                {
                    layout.Add(new TabEntry { Child = child });

                    continue;
                }

                if (!emitted.Add(tab.Group))
                {
                    continue;
                }

                var group = groups[tab.Group];
                var selectedChildren = new List<SerializedProperty>();

                foreach (var other in children)
                {
                    var otherTab = PropertyUtility.GetAttribute<TabAttribute>(other);

                    if (otherTab != null && otherTab.Group == tab.Group && !string.IsNullOrEmpty(otherTab.LabelMethod))
                    {
                        group.SetLabel(otherTab.Name, TabGroupUtility.ResolveLabel(PropertyUtility.GetTargetObjectWithProperty(other), otherTab));
                    }
                }

                layout.Add(new TabEntry { Toolbar = group, ToolbarNames = group.GetToolbarNames() });

                foreach (var other in children)
                {
                    var otherTab = PropertyUtility.GetAttribute<TabAttribute>(other);

                    if (otherTab != null && otherTab.Group == tab.Group && otherTab.Name == group.SelectedName)
                    {
                        selectedChildren.Add(other);
                    }
                }

                var labelWidth = selectedChildren.Count > 0 ? TabGroupUtility.MeasureLabelWidth(new List<SerializedProperty>(selectedChildren), 400f) : 0f;

                RunParents.TryGetValue(children, out var parent);

                var i = 0;

                while (i < selectedChildren.Count)
                {
                    var foldout = parent != null ? PropertyUtility.GetAttribute<FoldoutAttribute>(selectedChildren[i]) : null;

                    if (foldout != null)
                    {
                        var run = new List<SerializedProperty>();

                        while (i < selectedChildren.Count && PropertyUtility.GetAttribute<FoldoutAttribute>(selectedChildren[i])?.Name == foldout.Name)
                        {
                            run.Add(selectedChildren[i]);
                            i++;
                        }

                        layout.Add(new TabEntry { Foldout = foldout, FoldoutChildren = run, Parent = parent });

                        continue;
                    }

                    var start = i;

                    while (i < selectedChildren.Count && (parent == null || PropertyUtility.GetAttribute<FoldoutAttribute>(selectedChildren[i]) == null))
                    {
                        i++;
                    }

                    for (var j = start; j < i; j++)
                    {
                        layout.Add(new TabEntry
                        {
                            Child = selectedChildren[j],
                            InBox = true,
                            BoxStart = j == start,
                            BoxEnd = j == i - 1,
                            LabelWidth = labelWidth
                        });
                    }
                }
            }

            return layout;
        }

        private static void DrawTabbedChildren(Rect rect, List<TabEntry> layout, ref float y)
        {
            var previousOverride = _labelWidthOverride;

            for (var i = 0; i < layout.Count; i++)
            {
                var entry = layout[i];

                if (entry.Toolbar != null)
                {
                    var toolbarRect = new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight);
                    var selected = GUI.Toolbar(toolbarRect, entry.Toolbar.Selected, entry.ToolbarNames);

                    y += EditorGUIUtility.singleLineHeight;

                    if (selected != entry.Toolbar.Selected)
                    {
                        entry.Toolbar.Select(selected);
                        GUI.changed = true;
                        ClearHeightCache();

                        return;
                    }

                    continue;
                }

                if (entry.FoldoutChildren != null)
                {
                    DrawFoldoutRun(rect, entry.Parent, entry.Foldout, entry.FoldoutChildren, ref y);

                    continue;
                }

                if (entry.BoxStart)
                {
                    var boxHeight = TabGroupUtility.BoxPaddingVertical * 2f;

                    for (var j = i; j < layout.Count; j++)
                    {
                        boxHeight += GetHeight(layout[j].Child) + EditorGUIUtility.standardVerticalSpacing;

                        if (layout[j].BoxEnd)
                        {
                            break;
                        }
                    }

                    TabGroupUtility.DrawBox(new Rect(rect.x, y, rect.width, boxHeight));

                    y += TabGroupUtility.BoxPaddingVertical;
                }

                var title = GetCustomTitle(entry.Child);
                var height = GetHeight(entry.Child);
                var insetLeft = entry.InBox ? TabGroupUtility.BoxPaddingLeft : 0f;
                var insetRight = entry.InBox ? TabGroupUtility.BoxPaddingRight : 0f;
                var fieldRect = new Rect(rect.x + insetLeft, y, rect.width - insetLeft - insetRight, height);

                if (title != null)
                {
                    DrawTitle(rect, title.Title, y);
                    y += GetTitleHeight(entry.Child);
                    fieldRect.y = y;
                }

                _labelWidthOverride = entry.InBox ? entry.LabelWidth : GetWidestLabel(new List<SerializedProperty> { entry.Child });
                Field(fieldRect, entry.Child);
                y += height + EditorGUIUtility.standardVerticalSpacing;

                if (entry.BoxEnd)
                {
                    y += TabGroupUtility.BoxPaddingVertical;
                }
            }

            _labelWidthOverride = previousOverride;
        }

        // Leaf fields get their Title from the PropertyField decorator; custom-drawn ones bypass it, so it is drawn here
        private static TitleAttribute GetCustomTitle(SerializedProperty child)
        {
            var title = PropertyUtility.GetAttribute<TitleAttribute>(child);

            // Checking the cached attribute first avoids the allocating structural checks in IsLeaf
            return title == null || IsLeaf(child) ? null : title;
        }

        private static float GetTitleHeight(SerializedProperty child)
        {
            if (GetCustomTitle(child) == null)
            {
                return 0f;
            }

            return EditorGUIUtility.singleLineHeight + InspectorStyle.TitlePadding * 2f + InspectorStyle.TitleLineHeight + InspectorStyle.TitleBottomMargin;
        }

        private static void DrawTitle(Rect rect, string title, float y)
        {
            var labelRect = new Rect(rect.x, y + InspectorStyle.TitlePadding, rect.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            var lineRect = new Rect(rect.x, labelRect.yMax + 1f, rect.width, InspectorStyle.TitleLineHeight);

            EditorGUI.DrawRect(lineRect, InspectorStyle.TitleLine);
        }

        private static List<(FoldoutAttribute Foldout, List<SerializedProperty> Children)> GetRuns(SerializedProperty property)
        {
            var key = (property.serializedObject.GetHashCode(), property.propertyPath, PropertyUtility.ForceVisible ? 1 : 0);

            if (RunsCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var runs = ComputeRuns(property);

            RunsCache[key] = runs;

            return runs;
        }

        private static readonly Dictionary<List<SerializedProperty>, SerializedProperty> RunParents = new();

        private static List<(FoldoutAttribute Foldout, List<SerializedProperty> Children)> ComputeRuns(SerializedProperty property)
        {
            var runs = new List<(FoldoutAttribute Foldout, List<SerializedProperty> Children)>();

            // Keep every tabbed field next to the first one so they share a single toolbar
            var ordered = new List<SerializedProperty>();
            var firstTabIndex = -1;

            foreach (var child in GetChildren(property))
            {
                var isTab = PropertyUtility.GetAttribute<TabAttribute>(child) != null;

                if (!isTab)
                {
                    ordered.Add(child);
                }
                else if (firstTabIndex < 0)
                {
                    ordered.Add(child);
                    firstTabIndex = ordered.Count;
                }
                else
                {
                    ordered.Insert(firstTabIndex, child);
                    firstTabIndex++;
                }
            }

            foreach (var child in ordered)
            {
                // Tabbed fields stay in the tab run; their [Foldout] is resolved inside the selected tab
                var foldout = PropertyUtility.GetAttribute<TabAttribute>(child) != null ? null : PropertyUtility.GetAttribute<FoldoutAttribute>(child);

                if (runs.Count > 0 && runs[runs.Count - 1].Foldout?.Name == foldout?.Name && (foldout != null || runs[runs.Count - 1].Foldout == null))
                {
                    runs[runs.Count - 1].Children.Add(child);
                }
                else
                {
                    var list = new List<SerializedProperty> { child };

                    runs.Add((foldout, list));
                    RunParents[list] = property;
                }
            }

            return runs;
        }

        private static string GetFoldoutKey(SerializedProperty property, string name)
        {
            var target = property.serializedObject.targetObject;

            return $"MyListDrawer.{target.GetType().FullName}.{property.propertyPath}.{name}";
        }

        private static bool IsFoldoutOpen(SerializedProperty property, string name)
        {
            return SessionState.GetBool(GetFoldoutKey(property, name), false);
        }

        private static void DrawChildren(Rect rect, List<SerializedProperty> children, ref float y)
        {
            var tabLayout = GetTabLayout(children);

            if (tabLayout.Count > 0)
            {
                DrawTabbedChildren(rect, tabLayout, ref y);

                return;
            }

            var previousOverride = _labelWidthOverride;
            var widest = GetWidestLabel(children);

            foreach (var child in children)
            {
                var height = GetHeight(child);
                var title = GetCustomTitle(child);

                if (title != null)
                {
                    DrawTitle(rect, title.Title, y);
                    y += GetTitleHeight(child);
                }

                _labelWidthOverride = widest;
                Field(new Rect(rect.x, y, rect.width, height), child);
                y += height + EditorGUIUtility.standardVerticalSpacing;
            }

            _labelWidthOverride = previousOverride;
        }

        private static readonly Dictionary<List<SerializedProperty>, float> WidestLabelCache = new();

        private static float GetWidestLabel(List<SerializedProperty> children)
        {
            if (WidestLabelCache.TryGetValue(children, out var cached))
            {
                return cached;
            }

            var widest = ComputeWidestLabel(children);

            WidestLabelCache[children] = widest;

            return widest;
        }

        private static float ComputeWidestLabel(List<SerializedProperty> children)
        {
            var widest = 0f;

            foreach (var child in children)
            {
                if (IsLeaf(child))
                {
                    widest = Mathf.Max(widest, GetLabelWidth(PropertyUtility.GetLabel(child).text));
                }
            }

            return widest;
        }

        private static readonly Dictionary<string, float> LabelWidthCache = new();
        private static readonly GUIContent MeasureContent = new GUIContent();

        private static float GetLabelWidth(string text)
        {
            if (!LabelWidthCache.TryGetValue(text, out var width))
            {
                MeasureContent.text = text;
                width = EditorStyles.boldLabel.CalcSize(MeasureContent).x + 8f;
                LabelWidthCache[text] = width;
            }

            return width;
        }

        private static float _labelWidthOverride;

        /// <summary>Label width applied by <see cref="Field"/> to leaf fields; 0 means automatic default.</summary>
        public static float LabelWidthOverride
        {
            get => _labelWidthOverride;
            set => _labelWidthOverride = value;
        }

        /// <summary>Measures the widest leaf label of the given properties.</summary>
        public static float MeasureWidestLabel(List<SerializedProperty> properties)
        {
            return ComputeWidestLabel(properties);
        }

        private static bool IsLeaf(SerializedProperty property)
        {
            return !IsDictionary(property) && !IsList(property) && !IsContainer(property);
        }

        private static bool DrawFoldoutHeader(Rect rect, bool open, FoldoutAttribute foldout, SerializedProperty toggle, bool bold)
        {
            if (toggle == null)
            {
                return EditorGUI.Foldout(rect, open, foldout.Name, true);
            }

            var boolTitle = PropertyUtility.GetAttribute<BoolTitleAttribute>(toggle);
            var content = new GUIContent(boolTitle.Title ?? toggle.displayName, toggle.tooltip);
            var arrowWidth = InspectorStyle.BoolTitleArrowWidth;

            open = EditorGUI.Foldout(new Rect(rect.x, rect.y, arrowWidth, rect.height), open, GUIContent.none, true);

            EditorGUI.BeginProperty(rect, content, toggle);
            var wasEnabled = toggle.boolValue;

            toggle.boolValue = EditorGUI.ToggleLeft(new Rect(rect.x + arrowWidth, rect.y, rect.width - arrowWidth, rect.height), content, toggle.boolValue, bold ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUI.EndProperty();

            return wasEnabled != toggle.boolValue ? toggle.boolValue : open;
        }

        private static void DrawBody(Rect rect, List<SerializedProperty> body, SerializedProperty toggle, ref float y)
        {
            if (toggle == null)
            {
                DrawChildren(rect, body, ref y);

                return;
            }

            var previous = PropertyUtility.ForceVisible;
            var enabled = toggle.boolValue;

            PropertyUtility.ForceVisible = previous || !enabled;

            using (new EditorGUI.DisabledScope(!enabled))
            {
                DrawChildren(rect, body, ref y);
            }

            PropertyUtility.ForceVisible = previous;
        }

        private static void HandleInlineContextClick(Rect headerRect, List<SerializedProperty> body)
        {
            if (Event.current.type != EventType.ContextClick || !headerRect.Contains(Event.current.mousePosition))
            {
                return;
            }

            GenericMenu menu = null;

            for (var i = 0; i < body.Count; i++)
            {
                var child = body[i];

                if (child.propertyType != SerializedPropertyType.Generic || child.isArray || PropertyUtility.GetAttribute<InlinePropertyAttribute>(child) == null)
                {
                    continue;
                }

                menu ??= new GenericMenu();
                AddInlineCopyPaste(menu, child);
            }

            if (menu == null)
            {
                return;
            }

            menu.ShowAsContext();
            Event.current.Use();
        }

        public static void DrawFoldoutRun(Rect rect, SerializedProperty property, FoldoutAttribute foldout, List<SerializedProperty> children, ref float y)
        {
            var key = GetFoldoutKey(property, foldout.Name);
            var open = SessionState.GetBool(key, false);
            var toggle = GetToggle(children);
            var body = GetBody(property, foldout, children, toggle);

            if (foldout.Clean)
            {
                var line = new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight);

                HandleInlineContextClick(line, body);
                open = DrawFoldoutHeader(line, open, foldout, toggle, false);
                SessionState.SetBool(key, open);
                y += line.height + EditorGUIUtility.standardVerticalSpacing;

                if (open)
                {
                    var cleanRect = new Rect(rect.x + 8f, rect.y, rect.width - 8f, rect.height);

                    DrawBody(cleanRect, body, toggle, ref y);
                    DrawButtons(cleanRect, property, foldout, ref y);
                }

                return;
            }

            y += FoldoutTopSpace;

            var header = new Rect(rect.x, y, rect.width, BoxHeaderHeight);
            var headerColor = InspectorStyle.HeaderBackground;
            var contentColor = InspectorStyle.ContentBackground;

            EditorGUI.DrawRect(header, headerColor);

            var foldoutRect = new Rect(header.x + 18f, header.y + (BoxHeaderHeight - EditorGUIUtility.singleLineHeight) * 0.5f, header.width - 18f, EditorGUIUtility.singleLineHeight);

            HandleInlineContextClick(header, body);
            open = DrawFoldoutHeader(foldoutRect, open, foldout, toggle, true);
            SessionState.SetBool(key, open);
            y += BoxHeaderHeight;

            if (open)
            {
                var contentHeight = BoxPadding * 2f + GetBodyHeight(toggle, body) + GetButtonsHeight(property, foldout);
                var bodyRect = new Rect(rect.x + ContainerIndent, rect.y, rect.width - ContainerIndent * 2f, rect.height);

                EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, contentHeight), contentColor);
                y += BoxPadding;

                DrawBody(bodyRect, body, toggle, ref y);
                DrawButtons(bodyRect, property, foldout, ref y);
                y += BoxPadding;
            }

            DrawBorder(new Rect(rect.x, header.y, rect.width, y - header.y));
            y += FoldoutBottomSpace;
        }

        private static void DrawContainer(Rect rect, SerializedProperty property, GUIContent label, bool flat, bool plain)
        {
            var y = rect.y;
            var customLabel = flat ? null : GetFoldoutLabelText(property);
            var extraHeight = string.IsNullOrEmpty(customLabel) ? 0f : GetFoldoutLabelExtraHeight(property);

            if (!string.IsNullOrEmpty(customLabel))
            {
                label = new GUIContent(customLabel);
            }

            if (plain && !flat)
            {
                var line = new Rect(rect.x + 16f, rect.y, rect.width - 16f, EditorGUIUtility.singleLineHeight + extraHeight);

                HandleContainerContextClick(line, property);
                property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label ?? PropertyUtility.GetLabel(property), true);

                if (!property.isExpanded)
                {
                    return;
                }

                y = line.yMax + EditorGUIUtility.standardVerticalSpacing;

                var plainInner =                                 new Rect(rect.x + 30f, rect.y, rect.width - 30f, rect.height);

                foreach (var run in GetRuns(property))
                {
                    if (run.Foldout == null)
                    {
                        DrawChildren(plainInner, run.Children, ref y);
                    }
                    else
                    {
                        DrawFoldoutRun(plainInner, property, run.Foldout, run.Children, ref y);
                    }
                }

                DrawLooseButtons(plainInner, property, ref y);

                return;
            }

            if (!flat)
            {
                rect.y += TopPadding;
                rect.height -= TopPadding;

                var header = new Rect(rect.x, rect.y, rect.width, HeaderHeight + HeaderPadding * 2f + extraHeight);

                EditorGUI.DrawRect(header, InspectorStyle.HeaderBackground);

                var line = new Rect(header.x + 18f, header.y + HeaderPadding + (HeaderHeight - EditorGUIUtility.singleLineHeight) * 0.5f, header.width - 18f, EditorGUIUtility.singleLineHeight + extraHeight);

                                HandleContainerContextClick(header, property);
                                property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label ?? PropertyUtility.GetLabel(property), true);

                if (!property.isExpanded)
                {
                    DrawBorder(header);

                    return;
                }

                var contentHeight = BoxPadding * 2f + GetRunsHeight(property);

                EditorGUI.DrawRect(new Rect(rect.x, header.yMax, rect.width, contentHeight), InspectorStyle.ContentBackground);
                DrawBorder(new Rect(rect.x, rect.y, rect.width, header.height + contentHeight));
                y = header.yMax + BoxPadding;
            }

            var indent = flat ? 0f : ContainerIndent;

            var inner = new Rect(rect.x + indent, rect.y, rect.width - indent * 2f, rect.height);

            foreach (var run in GetRuns(property))
            {
                if (run.Foldout == null)
                {
                    DrawChildren(inner, run.Children, ref y);
                }
                else
                {
                    DrawFoldoutRun(inner, property, run.Foldout, run.Children, ref y);
                }
            }

            DrawLooseButtons(inner, property, ref y);
        }

        public static void Draw(Rect rect, SerializedProperty property)
        {
            EnsureExpanded(property);

            var field = GetField(property);
            var settings = GetSettings(property);
            var isReference = field != null && field.GetCustomAttribute<SerializeReference>() != null;
            var elementType = GetElementType(field != null ? field.FieldType : null);
            var showFoldout = true;
            var showIndex = settings != null && settings.ShowIndexLabels;
            var flat = settings == null || !settings.ShowFoldout;
            var label = PropertyUtility.GetLabel(property);

            rect.height = GetListBodyHeight(property, settings);

            var headerBackground = new Rect(rect.x, rect.y, rect.width, HeaderHeight + HeaderPadding * 2f);
            var header = new Rect(rect.x, rect.y + HeaderPadding, rect.width, HeaderHeight);
            var addRect = new Rect(header.xMax - ButtonWidth, header.y + (header.height - 18f) * 0.5f, ButtonWidth, 18f);
            var countRect = new Rect(addRect.x - CountWidth - 4f, header.y, CountWidth, header.height);
            var labelRect = new Rect(header.x + 18f, header.y, countRect.x - header.x - 18f, header.height);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(headerBackground, InspectorStyle.HeaderBackground);
                DrawBorder(rect);
            }

            if (Event.current.type == EventType.ContextClick && headerBackground.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(property, -1);
                Event.current.Use();

                return;
            }

            if (showFoldout)
            {
                property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, true);
            }
            else
            {
                EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
            }

            var countStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, fontSize = 12 };

            if (property.arraySize == 0)
            {
                countStyle.normal.textColor = InspectorStyle.CountLabel;
            }

            EditorGUI.LabelField(countRect, property.arraySize == 0 ? "Empty" : (property.arraySize == 1 ? "1 Item" : property.arraySize + " Items"), countStyle);

            if (GUI.Button(addRect, EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.iconButton))
            {
                Add(property, settings, isReference, elementType);

                return;
            }

            HandleObjectDrop(rect, property, isReference, elementType);

            if (!IsExpanded(property, settings))
            {
                return;
            }

            var y = headerBackground.yMax + Spacing * 0.5f;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var key = property.serializedObject.targetObject.GetEntityId() + property.propertyPath;
            var elementRects = new List<Rect>();
            var visible = GetVisibleRect();
            var eventType = Event.current.type;
            var cull = (eventType == EventType.Repaint || eventType == EventType.Layout
                        || eventType == EventType.ScrollWheel || eventType == EventType.MouseMove
                        || eventType == EventType.Used || eventType == EventType.Ignore)
                       && GUIUtility.keyboardControl == 0
                       && GUIUtility.hotControl == 0
                       && !ContainsTable(elementType, 0);

            for (var i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var height = GetElementHeight(element, flat);
                var rowRect = new Rect(rect.x + 1f, y - Spacing * 0.5f, rect.width - 2f, height + Spacing + ItemPadding * 2f);

                if (cull && (rowRect.yMax < visible.yMin || rowRect.yMin > visible.yMax))
                {
                    elementRects.Add(rowRect);
                    y += height + Spacing + ItemPadding * 2f;

                    continue;
                }

                var fieldRect = new Rect(rect.x + ElementIndent, y + ItemPadding, rect.width - ElementIndent - ButtonWidth - 4f, height);
                var removeRect = new Rect(rect.xMax - ButtonWidth - 2f, y + ItemPadding + (height - 20f) * 0.5f, ButtonWidth, 20f);
                var handleRect = new Rect(rect.x + 6f, y + ItemPadding, 16f, height);

                elementRects.Add(rowRect);

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rowRect, i % 2 == 0 ? EvenRowColor : OddRowColor);
                    GUI.Label(handleRect, "=", HandleStyle);
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && handleRect.Contains(Event.current.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    _dragKey = key;
                    _dragIndex = i;
                    _dragTarget = i;
                    Event.current.Use();
                }

                EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);

                if (IsNullReference(element))
                {
                    var elementPath = element.propertyPath;
                    var serializedObject = property.serializedObject;
                    var buttonRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width, EditorGUIUtility.singleLineHeight);

                    if (EditorGUI.DropdownButton(buttonRect, new GUIContent($"[{i}] Select type..."), FocusType.Keyboard))
                    {
                        ShowTypeMenu(elementType, type =>
                        {
                            serializedObject.Update();
                            serializedObject.FindProperty(elementPath).managedReferenceValue = Activator.CreateInstance(type);
                            serializedObject.ApplyModifiedProperties();
                        });
                    }
                }
                else
                {
                    var elementLabel = showIndex ? new GUIContent(i.ToString()) : (element.displayName.StartsWith("Element ") ? GUIContent.none : new GUIContent(element.displayName));

                    Field(fieldRect, element, elementLabel, flat, true);
                }

                // Handled after the content so the innermost row consumes the click first
                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    ShowContextMenu(property, i);
                    Event.current.Use();

                    return;
                }

                if (GUI.Button(removeRect, "x", RemoveStyle))
                {
                    Remove(property, i);

                    return;
                }

                y += height + Spacing + ItemPadding * 2f;
            }

            HandleDrag(property, key, controlId, elementRects);
        }

        // Lets assets be dropped on a list of UnityEngine.Object references (a texture adds all of its sprites)
        private static void HandleObjectDrop(Rect rect, SerializedProperty property, bool isReference, Type elementType)
        {
            var current = Event.current;

            if (isReference || elementType == null || !typeof(UnityEngine.Object).IsAssignableFrom(elementType) || !GUI.enabled)
            {
                return;
            }

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform || !rect.Contains(current.mousePosition))
            {
                return;
            }

            var assets = new List<UnityEngine.Object>();

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj == null)
                {
                    continue;
                }

                if (elementType.IsInstanceOfType(obj))
                {
                    assets.Add(obj);
                }
                else if (elementType == typeof(Sprite) && obj is Texture2D)
                {
                    assets.AddRange(AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(obj)).OfType<Sprite>());
                }
            }

            if (assets.Count == 0)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (var asset in assets)
                {
                    property.arraySize++;
                    property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = asset;
                }

                GUI.changed = true;
            }

            current.Use();
        }

        private static void HandleDrag(SerializedProperty property, string key, int controlId, List<Rect> elementRects)
        {
            if (_dragKey != key || GUIUtility.hotControl != controlId || elementRects.Count == 0)
            {
                return;
            }

            var current = Event.current;

            if (current.type == EventType.MouseDrag)
            {
                var mouseY = current.mousePosition.y;
                var target = elementRects.Count - 1;

                for (var i = 0; i < elementRects.Count; i++)
                {
                    if (mouseY < elementRects[i].yMax)
                    {
                        target = i;

                        break;
                    }
                }

                _dragTarget = target;
                current.Use();
                GUI.changed = true;
            }
            else if (current.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;

                if (_dragTarget != _dragIndex && _dragIndex < property.arraySize && _dragTarget < property.arraySize)
                {
                    property.MoveArrayElement(_dragIndex, _dragTarget);
                    GUI.changed = true;
                }

                _dragKey = null;
                current.Use();
            }
            else if (current.type == EventType.Repaint && _dragTarget < elementRects.Count)
            {
                var targetRect = elementRects[_dragTarget];
                var lineY = _dragTarget > _dragIndex ? targetRect.yMax - 1f : targetRect.y;

                EditorGUI.DrawRect(new Rect(targetRect.x, lineY, targetRect.width, 2f), InspectorStyle.DragInsertionLine);
            }
        }

        public static void DrawOutline(Rect rect)
        {
            DrawBorder(rect);
        }

        private static void DrawBorder(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), BorderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BorderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), BorderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), BorderColor);
        }

        private static bool IsExpanded(SerializedProperty property, ListDrawerSettingsAttribute settings)
        {
            return property.isExpanded;
        }

        // The expanded state is kept by Unity (isExpanded), so it is not forced anymore
        private static void EnsureExpanded(SerializedProperty property)
        {
        }

        private static bool IsNullReference(SerializedProperty element)
        {
            return element.propertyType == SerializedPropertyType.ManagedReference && element.managedReferenceValue == null;
        }

        private static float GetElementHeight(SerializedProperty element, bool flat)
        {
            if (IsNullReference(element))
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return GetHeight(element, flat, true);
        }

        private static FieldInfo GetField(SerializedProperty property)
        {
            var owner = PropertyUtility.GetTargetObjectWithProperty(property);

            return owner == null ? null : ReflectionUtility.GetField(owner, property.name);
        }

        private static ListDrawerSettingsAttribute GetSettings(SerializedProperty property)
        {
            return PropertyUtility.GetAttribute<ListDrawerSettingsAttribute>(property);
        }

        private static Type GetElementType(Type type)
        {
            if (type == null)
            {
                return typeof(object);
            }

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

        private static void Remove(SerializedProperty array, int index)
        {
            var element = array.GetArrayElementAtIndex(index);

            if (element.propertyType == SerializedPropertyType.ObjectReference && element.objectReferenceValue != null)
            {
                element.objectReferenceValue = null;
            }

            array.DeleteArrayElementAtIndex(index);
            GUI.changed = true;
        }

        private static void Add(SerializedProperty array, ListDrawerSettingsAttribute settings, bool isReference, Type elementType)
        {
            var serializedObject = array.serializedObject;

            ExpandForAdd(array);

            if (settings != null && !string.IsNullOrEmpty(settings.CustomAddFunction))
            {
                var owner = PropertyUtility.GetTargetObjectWithProperty(array);
                var method = owner?.GetType().GetMethod(
                    settings.CustomAddFunction,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (method != null)
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(serializedObject.targetObject, "Add element");
                    method.Invoke(method.IsStatic ? null : owner, null);
                    EditorUtility.SetDirty(serializedObject.targetObject);
                    serializedObject.Update();

                    return;
                }
            }

            if (!isReference)
            {
                var referenceField = FindReferenceField(elementType);

                if (referenceField == null || GetTypes(referenceField.FieldType).Count <= 1)
                {
                    AddDefaultElement(array, elementType);
                    GUI.changed = true;

                    return;
                }

                var arrayPath = array.propertyPath;

                ShowTypeMenu(referenceField.FieldType, type =>
                {
                    serializedObject.Update();

                    var list = serializedObject.FindProperty(arrayPath);

                    AddDefaultElement(list, elementType);
                    list.GetArrayElementAtIndex(list.arraySize - 1).FindPropertyRelative(referenceField.Name).managedReferenceValue = Activator.CreateInstance(type);
                    serializedObject.ApplyModifiedProperties();
                });

                return;
            }

            var types = GetTypes(elementType);

            if (types.Count == 1)
            {
                AddReference(array, types[0]);

                return;
            }

            var path = array.propertyPath;

            ShowTypeMenu(elementType, type =>
            {
                serializedObject.Update();
                AddReference(serializedObject.FindProperty(path), type);
                serializedObject.ApplyModifiedProperties();
            });
        }

        // Opens the collapsed list/dictionary and the [Foldout] group holding it so a newly added entry is visible
        private static void ExpandForAdd(SerializedProperty property)
        {
            property.isExpanded = true;

            var foldout = PropertyUtility.GetAttribute<FoldoutAttribute>(property);
            var path = property.propertyPath;
            var lastDot = path.LastIndexOf('.');

            if (foldout == null)
            {
                return;
            }

            var containerPath = lastDot < 0 ? string.Empty : path.Substring(0, lastDot);

            MyInspector.OpenFoldout(property.serializedObject.targetObject, containerPath, foldout.Name);

            var parent = lastDot < 0 ? null : property.serializedObject.FindProperty(containerPath);

            if (parent != null)
            {
                SessionState.SetBool(GetFoldoutKey(parent, foldout.Name), true);
            }
        }

        // Unity copies the last element when growing an array; this appends a fresh default element instead
        private static void AddDefaultElement(SerializedProperty array, Type elementType)
        {
            array.arraySize++;

            var element = array.GetArrayElementAtIndex(array.arraySize - 1);

            switch (element.propertyType)
            {
                case SerializedPropertyType.Generic:
                    if (elementType != null && !elementType.IsAbstract && !elementType.IsArray
                        && (elementType.IsValueType || elementType.GetConstructor(Type.EmptyTypes) != null))
                    {
                        element.boxedValue = Activator.CreateInstance(elementType);
                    }

                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    element.intValue = 0;

                    break;
                case SerializedPropertyType.Float:
                    element.floatValue = 0f;

                    break;
                case SerializedPropertyType.Boolean:
                    element.boolValue = false;

                    break;
                case SerializedPropertyType.String:
                    element.stringValue = string.Empty;

                    break;
                case SerializedPropertyType.ObjectReference:
                    element.objectReferenceValue = null;

                    break;
                case SerializedPropertyType.ManagedReference:
                    element.managedReferenceValue = null;

                    break;
            }
        }

        // First [SerializeReference] field of a plain serializable element, used to ask for its type when adding
        private static FieldInfo FindReferenceField(Type elementType)
        {
            if (elementType == null || elementType.IsPrimitive || elementType.IsEnum || elementType == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(elementType))
            {
                return null;
            }

            return elementType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.GetCustomAttribute<SerializeReference>() != null);
        }

        // Inserts a deep copy of the element right after it, so references are never shared
        private static void Duplicate(SerializedProperty list, int index)
        {
            var source = list.GetArrayElementAtIndex(index);
            object copy = null;

            if (source.propertyType == SerializedPropertyType.ManagedReference && source.managedReferenceValue != null)
            {
                copy = Clone(source.managedReferenceValue);
            }
            else if (source.propertyType == SerializedPropertyType.Generic && source.boxedValue != null)
            {
                copy = Clone(source.boxedValue);
            }

            list.InsertArrayElementAtIndex(index);

            if (copy == null)
            {
                return;
            }

            var target = list.GetArrayElementAtIndex(index + 1);

            if (target.propertyType == SerializedPropertyType.ManagedReference)
            {
                target.managedReferenceValue = copy;
            }
            else
            {
                target.boxedValue = copy;
            }
        }

        private static object Clone(object value)
        {
            var type = value.GetType();

            return JsonUtility.FromJson(JsonUtility.ToJson(value), type);
        }

        private static object _clipboard;
        private static string _clipboardKey;

        private static object CopyValue(SerializedProperty element)
        {
            if (element.propertyType == SerializedPropertyType.ManagedReference)
            {
                return element.managedReferenceValue == null ? null : Clone(element.managedReferenceValue);
            }

            if (element.propertyType == SerializedPropertyType.Generic)
            {
                return Snapshot(element);
            }

            return element.boxedValue;
        }

        private sealed class ListClipboard
        {
            public int Size;
            public PropertySnapshot Snapshot;
        }

        private sealed class PropertySnapshot
        {
            public readonly List<string> Paths = new List<string>();
            public readonly List<SerializedPropertyType> Types = new List<SerializedPropertyType>();
            public readonly List<object> Values = new List<object>();
        }

        // Reads every nested serialized field one by one, so no field is lost by a whole-object boxedValue copy
        private static PropertySnapshot Snapshot(SerializedProperty root)
        {
            var snapshot = new PropertySnapshot();
            var iterator = root.Copy();
            var end = root.GetEndProperty();
            var prefix = root.propertyPath + ".";
            var enterChildren = true;

            while (iterator.Next(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = true;

                var relative = iterator.propertyPath.StartsWith(prefix) ? iterator.propertyPath.Substring(prefix.Length) : iterator.propertyPath;

                if (iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic)
                {
                    snapshot.Paths.Add(relative);
                    snapshot.Types.Add(SerializedPropertyType.ArraySize);
                    snapshot.Values.Add(iterator.arraySize);

                    continue;
                }

                if (iterator.propertyType == SerializedPropertyType.ArraySize || iterator.propertyType == SerializedPropertyType.Generic)
                {
                    continue;
                }

                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    snapshot.Paths.Add(relative);
                    snapshot.Types.Add(iterator.propertyType);
                    snapshot.Values.Add(iterator.managedReferenceValue == null ? null : Clone(iterator.managedReferenceValue));
                    enterChildren = false;

                    continue;
                }

                snapshot.Paths.Add(relative);
                snapshot.Types.Add(iterator.propertyType);
                snapshot.Values.Add(iterator.boxedValue);
            }

            return snapshot;
        }

        private static void ApplySnapshot(SerializedProperty target, PropertySnapshot snapshot)
        {
            for (var i = 0; i < snapshot.Paths.Count; i++)
            {
                var property = target.FindPropertyRelative(snapshot.Paths[i]);

                if (property == null)
                {
                    continue;
                }

                var value = snapshot.Values[i];

                if (snapshot.Types[i] == SerializedPropertyType.ArraySize)
                {
                    property.arraySize = (int)value;
                }
                else if (snapshot.Types[i] == SerializedPropertyType.ManagedReference)
                {
                    property.managedReferenceValue = value == null ? null : Clone(value);
                }
                else if (value != null)
                {
                    property.boxedValue = value;
                }
            }
        }

        /// <summary>Shows Copy/Paste entries for the container itself when its own header is right-clicked.</summary>
        public static void HandleContainerContextClick(Rect headerRect, SerializedProperty property)
        {
            if (Event.current.type != EventType.ContextClick || !headerRect.Contains(Event.current.mousePosition)
                || property.propertyType != SerializedPropertyType.Generic || property.isArray)
            {
                return;
            }

            var menu = new GenericMenu();

            AddInlineCopyPaste(menu, property);
            menu.ShowAsContext();
            Event.current.Use();
        }

        // Adds "Copy <field>" / "Paste <field>" entries for an inline property; the snapshot is only taken when clicked
        public static void AddInlineCopyPaste(GenericMenu menu, SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            var path = property.propertyPath;
            var label = property.displayName;
            var key = "inline:" + property.type;

            menu.AddItem(new GUIContent("Copy " + label), false, () =>
            {
                var target = serializedObject.FindProperty(path);

                if (target == null)
                {
                    return;
                }

                _clipboard = Snapshot(target);
                _clipboardKey = key;
            });

            if (_clipboardKey == key && _clipboard is PropertySnapshot snapshot)
            {
                menu.AddItem(new GUIContent("Paste " + label), false, () =>
                {
                    serializedObject.Update();

                    var target = serializedObject.FindProperty(path);

                    if (target == null)
                    {
                        return;
                    }

                    ApplySnapshot(target, snapshot);
                    serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste " + label));
            }
        }


        private static void PasteValue(SerializedProperty element, object value)
        {
            if (element.propertyType == SerializedPropertyType.ManagedReference)
            {
                element.managedReferenceValue = value == null ? null : Clone(value);
            }
            else if (element.propertyType == SerializedPropertyType.Generic)
            {
                if (value is PropertySnapshot snapshot)
                {
                    ApplySnapshot(element, snapshot);
                }
            }
            else
            {
                element.boxedValue = value;
            }
        }

        private static void ShowContextMenu(SerializedProperty property, int index)
        {
            var serializedObject = property.serializedObject;
            var path = property.propertyPath;
            var menu = new GenericMenu();

            void Apply(Action<SerializedProperty> action)
            {
                serializedObject.Update();
                action(serializedObject.FindProperty(path));
                serializedObject.ApplyModifiedProperties();
            }

            var key = property.arrayElementType;
            var canPaste = _clipboardKey == key;

            if (index >= 0)
            {
                var copied = CopyValue(property.GetArrayElementAtIndex(index));

                menu.AddItem(new GUIContent("Copy"), false, () =>
                {
                    _clipboard = copied;
                    _clipboardKey = key;
                });

                if (canPaste)
                {
                    menu.AddItem(new GUIContent("Paste"), false, () => Apply(list => PasteValue(list.GetArrayElementAtIndex(index), _clipboard)));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste"));
                }

                menu.AddSeparator(string.Empty);
            }
            else
            {
                var listKey = "list:" + key;
                var listSize = property.arraySize;
                var listCopied = Snapshot(property);

                menu.AddItem(new GUIContent("Copy list"), false, () =>
                {
                    _clipboard = new ListClipboard { Size = listSize, Snapshot = listCopied };
                    _clipboardKey = listKey;
                });

                if (_clipboardKey == listKey && _clipboard is ListClipboard clipboardList)
                {
                    menu.AddItem(new GUIContent("Paste list"), false, () => Apply(list =>
                    {
                        list.arraySize = clipboardList.Size;
                        ApplySnapshot(list, clipboardList.Snapshot);
                    }));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste list"));
                }

                if (canPaste)
                {
                    menu.AddItem(new GUIContent("Paste as new element"), false, () => Apply(list =>
                    {
                        list.arraySize++;
                        PasteValue(list.GetArrayElementAtIndex(list.arraySize - 1), _clipboard);
                    }));
                }

                menu.AddSeparator(string.Empty);
            }

            if (index >= 0)
            {
                menu.AddItem(new GUIContent("Duplicate"), false, () => Apply(list => Duplicate(list, index)));
                menu.AddItem(new GUIContent("Delete"), false, () => Apply(list => Remove(list, index)));

                if (index > 0)
                {
                    menu.AddItem(new GUIContent("Move up"), false, () => Apply(list => list.MoveArrayElement(index, index - 1)));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move up"));
                }

                if (index < property.arraySize - 1)
                {
                    menu.AddItem(new GUIContent("Move down"), false, () => Apply(list => list.MoveArrayElement(index, index + 1)));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move down"));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Clear"), false, () => Apply(list => list.arraySize = 0));
            MyInspectorExtensions.InvokeCollectionContextMenu(menu, index >= 0 ? property.GetArrayElementAtIndex(index).propertyPath : path);
            menu.ShowAsContext();
        }

        private static void AddReference(SerializedProperty array, Type type)
        {
            array.arraySize++;
            array.GetArrayElementAtIndex(array.arraySize - 1).managedReferenceValue = Activator.CreateInstance(type);
            GUI.changed = true;
        }

        private static List<Type> GetTypes(Type baseType)
        {
            var types = TypeCache.GetTypesDerivedFrom(baseType).ToList();

            if (!baseType.IsInterface)
            {
                types.Add(baseType);
            }

            return types
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericType && !typeof(UnityEngine.Object).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToList();
        }

        private static void ShowTypeMenu(Type baseType, Action<Type> onSelected)
        {
            var types = GetTypes(baseType);
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
    }
}
