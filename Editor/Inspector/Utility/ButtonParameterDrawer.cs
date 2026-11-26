using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MyInspector.Editor
{
    /// <summary>
    /// Draws [Button] methods along with editable fields for their arguments.
    /// Edited values are editor-only state: they are not serialized and reset on domain reload.
    /// </summary>
    public static class ButtonParameterDrawer
    {
        private sealed class MethodData
        {
            public ParameterInfo[] Parameters;
            public GUIContent[] Labels;
            public bool[] Editable;
            public int EditableCount;
            public bool Invokable;
        }

        private static readonly Dictionary<MethodInfo, MethodData> MethodCache = new();
        private static readonly Dictionary<(object Owner, string Path, MethodInfo Method), object[]> ValueCache = new();

        /// <summary>
        /// True when every parameter can either be edited in the inspector or has a default value.
        /// </summary>
        public static bool IsInvokable(MethodInfo method)
        {
            return GetData(method).Invokable;
        }

        /// <summary>
        /// True when the method has at least one argument to edit. Callers can skip building a value key otherwise.
        /// </summary>
        public static bool HasEditableParameters(MethodInfo method)
        {
            return GetData(method).EditableCount > 0;
        }

        /// <summary>
        /// Height of the button, including its argument fields.
        /// </summary>
        public static float GetHeight(MethodInfo method)
        {
            var data = GetData(method);
            var height = EditorGUIUtility.singleLineHeight;

            if (data.EditableCount == 0)
            {
                return height;
            }

            for (var i = 0; i < data.Parameters.Length; i++)
            {
                if (data.Editable[i])
                {
                    height += GetFieldHeight(data.Parameters[i].ParameterType, data.Labels[i]) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height + InspectorStyle.ButtonParameterPadding * 2f;
        }

        /// <summary>
        /// Draws the argument fields and the button. The owner and path identify where edited values are stored.
        /// </summary>
        /// <returns>True when the button was clicked, with the arguments to invoke the method with.</returns>
        public static bool Draw(Rect rect, MethodInfo method, object owner, string path, string text, out object[] arguments, GUIStyle style = null)
        {
            var data = GetData(method);
            var values = data.EditableCount > 0 ? GetValues(data, method, owner, path) : null;
            var buttonRect = rect;

            if (values != null)
            {
                GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

                var padding = InspectorStyle.ButtonParameterPadding;
                var y = rect.y + padding;
                // Editing an argument must not flag the inspected object as modified
                var changed = GUI.changed;

                for (var i = 0; i < data.Parameters.Length; i++)
                {
                    if (!data.Editable[i])
                    {
                        continue;
                    }

                    var type = data.Parameters[i].ParameterType;
                    var height = GetFieldHeight(type, data.Labels[i]);

                    values[i] = DrawField(new Rect(rect.x + padding, y, rect.width - padding * 2f, height), data.Labels[i], type, values[i]);
                    y += height + EditorGUIUtility.standardVerticalSpacing;
                }

                GUI.changed = changed;
                buttonRect = new Rect(rect.x + padding, y, rect.width - padding * 2f, EditorGUIUtility.singleLineHeight);
            }

            if (!GUI.Button(buttonRect, text, style ?? GUI.skin.button))
            {
                arguments = null;

                return false;
            }

            arguments = new object[data.Parameters.Length];

            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = values != null && data.Editable[i] ? values[i] : GetDefaultValue(data.Parameters[i]);
            }

            return true;
        }

        /// <summary>
        /// Layout version of <see cref="Draw"/>.
        /// </summary>
        public static bool Draw_Layout(MethodInfo method, object owner, string path, string text, out object[] arguments, GUIStyle style = null)
        {
            var rect = EditorGUILayout.GetControlRect(false, GetHeight(method));

            return Draw(rect, method, owner, path, text, out arguments, style);
        }

        private static MethodData GetData(MethodInfo method)
        {
            if (MethodCache.TryGetValue(method, out var cached))
            {
                return cached;
            }

            var parameters = method.GetParameters();
            var data = new MethodData
            {
                Parameters = parameters,
                Labels = new GUIContent[parameters.Length],
                Editable = new bool[parameters.Length],
                Invokable = !method.ContainsGenericParameters
            };

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                data.Labels[i] = new GUIContent(ObjectNames.NicifyVariableName(parameter.Name));
                data.Editable[i] = IsSupported(parameter.ParameterType);

                if (data.Editable[i])
                {
                    data.EditableCount++;
                }
                else if (!parameter.IsOptional)
                {
                    data.Invokable = false;
                }
            }

            MethodCache[method] = data;

            return data;
        }

        private static object[] GetValues(MethodData data, MethodInfo method, object owner, string path)
        {
            var key = (owner, path, method);

            if (ValueCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var values = new object[data.Parameters.Length];

            for (var i = 0; i < values.Length; i++)
            {
                if (data.Editable[i])
                {
                    values[i] = GetInitialValue(data.Parameters[i]);
                }
            }

            ValueCache[key] = values;

            return values;
        }

        private static object GetDefaultValue(ParameterInfo parameter)
        {
            return parameter.HasDefaultValue ? parameter.DefaultValue : Type.Missing;
        }

        private static object GetInitialValue(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;
            var value = parameter.HasDefaultValue ? parameter.DefaultValue : null;

            if (value != null)
            {
                return type.IsEnum ? Enum.ToObject(type, value) : value;
            }

            if (type == typeof(string))
            {
                return string.Empty;
            }

            if (type == typeof(AnimationCurve))
            {
                return new AnimationCurve();
            }

            if (type == typeof(Gradient))
            {
                return new Gradient();
            }

            // Also covers "= default" on structs, whose reflected default value is null
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static bool IsSupported(Type type)
        {
            return type.IsEnum
                || typeof(UnityEngine.Object).IsAssignableFrom(type)
                || type == typeof(bool)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(string)
                || type == typeof(Vector2)
                || type == typeof(Vector3)
                || type == typeof(Vector4)
                || type == typeof(Vector2Int)
                || type == typeof(Vector3Int)
                || type == typeof(Color)
                || type == typeof(Rect)
                || type == typeof(RectInt)
                || type == typeof(Bounds)
                || type == typeof(BoundsInt)
                || type == typeof(LayerMask)
                || type == typeof(AnimationCurve)
                || type == typeof(Gradient);
        }

        private static float GetFieldHeight(Type type, GUIContent label)
        {
            // These fields wrap on several lines when the inspector is narrow
            if (type == typeof(Vector2))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, label);
            }

            if (type == typeof(Vector3))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector3, label);
            }

            if (type == typeof(Vector4))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector4, label);
            }

            if (type == typeof(Vector2Int))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2Int, label);
            }

            if (type == typeof(Vector3Int))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector3Int, label);
            }

            if (type == typeof(Rect))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Rect, label);
            }

            if (type == typeof(RectInt))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.RectInt, label);
            }

            if (type == typeof(Bounds))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Bounds, label);
            }

            if (type == typeof(BoundsInt))
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.BoundsInt, label);
            }

            return EditorGUIUtility.singleLineHeight;
        }

        private static object DrawField(Rect rect, GUIContent label, Type type, object value)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return EditorGUI.ObjectField(rect, label, value as UnityEngine.Object, type, true);
            }

            if (type.IsEnum)
            {
                return type.IsDefined(typeof(FlagsAttribute), false)
                    ? EditorGUI.EnumFlagsField(rect, label, (Enum)value)
                    : EditorGUI.EnumPopup(rect, label, (Enum)value);
            }

            if (type == typeof(bool))
            {
                return EditorGUI.Toggle(rect, label, (bool)value);
            }

            if (type == typeof(int))
            {
                return EditorGUI.IntField(rect, label, (int)value);
            }

            if (type == typeof(long))
            {
                return EditorGUI.LongField(rect, label, (long)value);
            }

            if (type == typeof(float))
            {
                return EditorGUI.FloatField(rect, label, (float)value);
            }

            if (type == typeof(double))
            {
                return EditorGUI.DoubleField(rect, label, (double)value);
            }

            if (type == typeof(string))
            {
                return EditorGUI.TextField(rect, label, (string)value);
            }

            if (type == typeof(Vector2))
            {
                return EditorGUI.Vector2Field(rect, label, (Vector2)value);
            }

            if (type == typeof(Vector3))
            {
                return EditorGUI.Vector3Field(rect, label, (Vector3)value);
            }

            if (type == typeof(Vector4))
            {
                return EditorGUI.Vector4Field(rect, label, (Vector4)value);
            }

            if (type == typeof(Vector2Int))
            {
                return EditorGUI.Vector2IntField(rect, label, (Vector2Int)value);
            }

            if (type == typeof(Vector3Int))
            {
                return EditorGUI.Vector3IntField(rect, label, (Vector3Int)value);
            }

            if (type == typeof(Color))
            {
                return EditorGUI.ColorField(rect, label, (Color)value);
            }

            if (type == typeof(Rect))
            {
                return EditorGUI.RectField(rect, label, (Rect)value);
            }

            if (type == typeof(RectInt))
            {
                return EditorGUI.RectIntField(rect, label, (RectInt)value);
            }

            if (type == typeof(Bounds))
            {
                return EditorGUI.BoundsField(rect, label, (Bounds)value);
            }

            if (type == typeof(BoundsInt))
            {
                return EditorGUI.BoundsIntField(rect, label, (BoundsInt)value);
            }

            if (type == typeof(LayerMask))
            {
                var mask = InternalEditorUtility.LayerMaskToConcatenatedLayersMask((LayerMask)value);

                mask = EditorGUI.MaskField(rect, label, mask, InternalEditorUtility.layers);

                return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(mask);
            }

            if (type == typeof(AnimationCurve))
            {
                return EditorGUI.CurveField(rect, label, (AnimationCurve)value);
            }

            if (type == typeof(Gradient))
            {
                return EditorGUI.GradientField(rect, label, (Gradient)value);
            }

            return value;
        }
    }
}
