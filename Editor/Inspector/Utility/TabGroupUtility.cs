using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyInspector.Editor
{
    /// <summary>
    /// Shared logic for Tab groups, used by the standard inspector (layout based) and by custom property drawers (rect based).
    /// </summary>
    public static class TabGroupUtility
    {
        public const float BoxPaddingLeft = InspectorStyle.TabBoxPaddingLeft;
        public const float BoxPaddingRight = InspectorStyle.TabBoxPaddingRight;
        public const float BoxPaddingVertical = InspectorStyle.TabBoxPaddingVertical;

        private static GUIStyle _boxStyle;

        public sealed class TabGroup
        {
            public string Key;
            public string StateKey;
            public List<string> Names = new();
            public int Selected;

            // Display text per tab, same order as Names. Null entries fall back to the name.
            public string[] Labels;

            public string SelectedName => Names[Selected];

            public void SetLabel(string name, string label)
            {
                var index = Names.IndexOf(name);

                if (index < 0 || string.IsNullOrEmpty(label))
                {
                    return;
                }

                Labels ??= new string[Names.Count];
                Labels[index] = label;
            }

            public string[] GetToolbarNames()
            {
                var result = new string[Names.Count];

                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = Labels != null && Labels[i] != null ? Labels[i] : Names[i];
                }

                return result;
            }

            public void Select(int index)
            {
                Selected = index;
                SessionState.SetInt(StateKey, index);
            }
        }

        public static GUIStyle BoxStyle
        {
            get
            {
                return _boxStyle ??= new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset((int) BoxPaddingLeft, (int) BoxPaddingRight, (int) BoxPaddingVertical, (int) BoxPaddingVertical)
                };
            }
        }

        public static TabGroup Create(string key, List<string> names, string stateKey)
        {
            var distinct = new List<string>(names.Count);

            foreach (var name in names)
            {
                if (!distinct.Contains(name))
                {
                    distinct.Add(name);
                }
            }

            var group = new TabGroup { Key = key, StateKey = stateKey, Names = distinct };

            group.Selected = Mathf.Clamp(SessionState.GetInt(stateKey, 0), 0, Mathf.Max(0, group.Names.Count - 1));

            return group;
        }

        /// <summary>
        /// Builds one group per Tab group key, with the names of its tabs in declaration order.
        /// </summary>
        public static Dictionary<string, TabGroup> BuildGroups(IEnumerable<TabAttribute> tabs, Func<string, string> stateKeyFor)
        {
            var namesByGroup = new Dictionary<string, List<string>>();

            foreach (var tab in tabs)
            {
                if (!namesByGroup.TryGetValue(tab.Group, out var names))
                {
                    names = new List<string>();
                    namesByGroup[tab.Group] = names;
                }

                names.Add(tab.Name);
            }

            var groups = new Dictionary<string, TabGroup>(namesByGroup.Count);

            foreach (var pair in namesByGroup)
            {
                groups[pair.Key] = Create(pair.Key, pair.Value, stateKeyFor(pair.Key));
            }

            return groups;
        }

        /// <summary>
        /// Calls the LabelMethod of the tab on its owner object. Returns null when there is none or it does not return a string.
        /// </summary>
        public static string ResolveLabel(object owner, TabAttribute tab)
        {
            if (owner == null || tab == null || string.IsNullOrEmpty(tab.LabelMethod))
            {
                return null;
            }

            var method = ReflectionUtility.GetMethod(owner, tab.LabelMethod);

            if (method == null || method.GetParameters().Length != 0)
            {
                return null;
            }

            return method.Invoke(owner, null) as string;
        }

        /// <summary>
        /// Width of the widest label among the given properties, capped relatively to the available width.
        /// Returns 0 when there is nothing to measure.
        /// </summary>
        public static float MeasureLabelWidth(List<SerializedProperty> properties, float availableWidth)
        {
            properties.RemoveAll(p => p == null);

            var widest = MyListDrawer.MeasureWidestLabel(properties);

            return widest > 0f ? Mathf.Min(widest, availableWidth * 0.6f) : 0f;
        }
    }
}
