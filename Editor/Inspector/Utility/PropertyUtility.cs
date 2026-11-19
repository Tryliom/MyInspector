using UnityEditor;
using System.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MyInspector.Editor
{
    public static class PropertyUtility
    {
        public static bool TryGetNumericValue(SerializedProperty property, string memberName, out float value)
        {
            value = 0f;

            object target = GetTargetObjectWithProperty(property);
            if (target == null)
            {
                return false;
            }

            FieldInfo field = ReflectionUtility.GetField(target, memberName);
            if (field != null)
            {
                return TryConvertNumericValue(field.GetValue(target), out value);
            }

            PropertyInfo prop = ReflectionUtility.GetProperty(target, memberName);
            if (prop != null)
            {
                return TryConvertNumericValue(prop.GetValue(target), out value);
            }

            MethodInfo method = ReflectionUtility.GetMethod(target, memberName);
            if (method != null && method.GetParameters().Length == 0)
            {
                return TryConvertNumericValue(method.Invoke(target, null), out value);
            }

            return false;
        }

        public static T GetAttribute<T>(SerializedProperty property) where T : class
        {
            T[] attributes = GetAttributes<T>(property);
            return (attributes.Length > 0) ? attributes[0] : null;
        }

        public static T[] GetAttributes<T>(SerializedProperty property) where T : class
        {
            var fieldKey = (property.serializedObject.targetObject.GetHashCode(), property.propertyPath);

            if (!FieldInfoCache.TryGetValue(fieldKey, out var fieldInfo))
            {
                fieldInfo = ReflectionUtility.GetField(GetTargetObjectWithProperty(property), property.name);
                FieldInfoCache[fieldKey] = fieldInfo;
            }

            if (fieldInfo == null)
            {
                return System.Array.Empty<T>();
            }

            var key = (fieldInfo, typeof(T));

            if (!AttributeCache.TryGetValue(key, out var cached))
            {
                cached = (T[])fieldInfo.GetCustomAttributes(typeof(T), true);
                AttributeCache[key] = cached;
            }

            return (T[])cached;
        }

        // Per-event cache, cleared with the visibility cache.
        private static readonly Dictionary<(int, string), FieldInfo> FieldInfoCache = new();

        private static readonly System.Collections.Generic.Dictionary<(FieldInfo, System.Type), System.Array> AttributeCache =
            new System.Collections.Generic.Dictionary<(FieldInfo, System.Type), System.Array>();

        public static GUIContent GetLabel(SerializedProperty property)
        {
            LabelAttribute labelAttribute = GetAttribute<LabelAttribute>(property);
            string labelText = (labelAttribute == null)
                ? property.displayName
                : labelAttribute.Label;

            GUIContent label = new GUIContent(labelText);
            return label;
        }

        public static void CallOnValueChangedCallbacks(SerializedProperty property)
        {
            OnValueChangedAttribute[] onValueChangedAttributes = GetAttributes<OnValueChangedAttribute>(property);
            if (onValueChangedAttributes.Length == 0)
            {
                return;
            }

            object target = GetTargetObjectWithProperty(property);
            property.serializedObject.ApplyModifiedProperties(); // We must apply modifications so that the new value is updated in the serialized object

            foreach (var onValueChangedAttribute in onValueChangedAttributes)
            {
                MethodInfo callbackMethod = ReflectionUtility.GetMethod(target, onValueChangedAttribute.CallbackName);
                if (callbackMethod != null &&
                    callbackMethod.ReturnType == typeof(void) &&
                    callbackMethod.GetParameters().Length == 0)
                {
                    callbackMethod.Invoke(target, new object[] { });
                }
                else
                {
                    string warning = string.Format(
                        "{0} can invoke only methods with 'void' return type and 0 parameters",
                        onValueChangedAttribute.GetType().Name);

                    Debug.LogWarning(warning, property.serializedObject.targetObject);
                }
            }
        }

        public static bool IsEnabled(SerializedProperty property)
        {
            ReadOnlyAttribute readOnlyAttribute = GetAttribute<ReadOnlyAttribute>(property);
            if (readOnlyAttribute != null)
            {
                return false;
            }

            EnableIfAttributeBase enableIfAttribute = GetAttribute<EnableIfAttributeBase>(property);
            if (enableIfAttribute == null)
            {
                return true;
            }

            object target = GetTargetObjectWithProperty(property);

            // deal with enum conditions
            if (enableIfAttribute.EnumValue != null)
            {
                Enum value = GetEnumValue(target, enableIfAttribute.Conditions[0]);
                if (value != null)
                {
                    bool matched = value.GetType().GetCustomAttribute<FlagsAttribute>() == null
                        ? enableIfAttribute.EnumValue.Equals(value)
                        : value.HasFlag(enableIfAttribute.EnumValue);

                    return matched != enableIfAttribute.Inverted;
                }

                string message = enableIfAttribute.GetType().Name + " needs a valid enum field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }

            // deal with normal conditions
            List<bool> conditionValues = GetConditionValues(target, enableIfAttribute.Conditions);
            if (conditionValues.Count > 0)
            {
                bool enabled = GetConditionsFlag(conditionValues, enableIfAttribute.ConditionOperator, enableIfAttribute.Inverted);
                return enabled;
            }
            else
            {
                string message = enableIfAttribute.GetType().Name + " needs a valid boolean condition field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }
        }

        /// <summary>When true, ShowIf/HideIf conditions are ignored (used to list fields of a disabled BoolTitle foldout).</summary>
        public static bool ForceVisible;

        private static readonly Dictionary<(int Object, string Path), bool> VisibilityCache = new();

        /// <summary>Clears the per-event visibility cache. Called at the start of every inspector OnGUI.</summary>
        public static void ClearVisibilityCache()
        {
            VisibilityCache.Clear();
            FieldInfoCache.Clear();
        }

        public static bool IsVisible(SerializedProperty property)
        {
            if (ForceVisible)
            {
                return true;
            }

            var key = (property.serializedObject.GetHashCode(), property.propertyPath);

            if (VisibilityCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var result = ComputeIsVisible(property);

            VisibilityCache[key] = result;

            return result;
        }

        private static bool ComputeIsVisible(SerializedProperty property)
        {
            ShowIfAttributeBase showIfAttribute = GetAttribute<ShowIfAttributeBase>(property);
            if (showIfAttribute == null)
            {
                return true;
            }

            object target = GetTargetObjectWithProperty(property);

            // deal with enum conditions
            if (showIfAttribute.EnumValue != null)
            {
                Enum value = GetEnumValue(target, showIfAttribute.Conditions[0]);
                if (value != null)
                {
                    bool matched = value.GetType().GetCustomAttribute<FlagsAttribute>() == null
                        ? showIfAttribute.EnumValue.Equals(value)
                        : value.HasFlag(showIfAttribute.EnumValue);

                    return matched != showIfAttribute.Inverted;
                }

                string message = showIfAttribute.GetType().Name + " needs a valid enum field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }

            // deal with normal conditions
            List<bool> conditionValues = GetConditionValues(target, showIfAttribute.Conditions);
            if (conditionValues.Count > 0)
            {
                bool enabled = GetConditionsFlag(conditionValues, showIfAttribute.ConditionOperator, showIfAttribute.Inverted);
                return enabled;
            }
            else
            {
                string message = showIfAttribute.GetType().Name + " needs a valid boolean condition field, property or method name to work";
                Debug.LogWarning(message, property.serializedObject.targetObject);

                return false;
            }
        }

        /// <summary>
        ///		Gets an enum value from reflection.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="enumName">Name of a field, property, or method that returns an enum.</param>
        /// <returns>Null if can't find an enum value.</returns>
        internal static Enum GetEnumValue(object target, string enumName)
        {
            FieldInfo enumField = ReflectionUtility.GetField(target, enumName);
            if (enumField != null && enumField.FieldType.IsSubclassOf(typeof(Enum)))
            {
                return (Enum)enumField.GetValue(target);
            }

            PropertyInfo enumProperty = ReflectionUtility.GetProperty(target, enumName);
            if (enumProperty != null && enumProperty.PropertyType.IsSubclassOf(typeof(Enum)))
            {
                return (Enum)enumProperty.GetValue(target);
            }

            MethodInfo enumMethod = ReflectionUtility.GetMethod(target, enumName);
            if (enumMethod != null && enumMethod.ReturnType.IsSubclassOf(typeof(Enum)))
            {
                return (Enum)enumMethod.Invoke(target, null);
            }

            return null;
        }

        internal static List<bool> GetConditionValues(object target, string[] conditions)
        {
            List<bool> conditionValues = new List<bool>();
            foreach (var condition in conditions)
            {
                FieldInfo conditionField = ReflectionUtility.GetField(target, condition);
                if (conditionField != null &&
                    conditionField.FieldType == typeof(bool))
                {
                    conditionValues.Add((bool)conditionField.GetValue(target));
                }

                PropertyInfo conditionProperty = ReflectionUtility.GetProperty(target, condition);
                if (conditionProperty != null &&
                    conditionProperty.PropertyType == typeof(bool))
                {
                    conditionValues.Add((bool)conditionProperty.GetValue(target));
                }

                MethodInfo conditionMethod = ReflectionUtility.GetMethod(target, condition);
                if (conditionMethod != null &&
                    conditionMethod.ReturnType == typeof(bool) &&
                    conditionMethod.GetParameters().Length == 0)
                {
                    conditionValues.Add((bool)conditionMethod.Invoke(target, null));
                }
            }

            return conditionValues;
        }

        internal static bool GetConditionsFlag(List<bool> conditionValues, EConditionOperator conditionOperator, bool invert)
        {
            bool flag;
            if (conditionOperator == EConditionOperator.And)
            {
                flag = true;
                foreach (var value in conditionValues)
                {
                    flag = flag && value;
                }
            }
            else
            {
                flag = false;
                foreach (var value in conditionValues)
                {
                    flag = flag || value;
                }
            }

            if (invert)
            {
                flag = !flag;
            }

            return flag;
        }

        public static Type GetPropertyType(SerializedProperty property)
        {
            object obj = GetTargetObjectOfProperty(property);
            Type objType = obj.GetType();

            return objType;
        }

        /// <summary>
        /// Gets the object the property represents.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public static object GetTargetObjectOfProperty(SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            string path = property.propertyPath.Replace(".Array.data[", "[");
            object obj = property.serializedObject.targetObject;
            string[] elements = path.Split('.');

            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("["));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }

            return obj;
        }

        /// <summary>
        /// Gets the object that the property is a member of
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private static readonly Dictionary<string, (string Name, int Index)[]> PathCache = new();
        private static readonly Dictionary<(Type, string), MemberInfo> MemberCache = new();

        private static (string Name, int Index)[] ParsePath(string propertyPath)
        {
            if (PathCache.TryGetValue(propertyPath, out var parsed))
            {
                return parsed;
            }

            var elements = propertyPath.Replace(".Array.data[", "[").Split('.');

            parsed = new (string Name, int Index)[elements.Length - 1];

            for (var i = 0; i < parsed.Length; i++)
            {
                var element = elements[i];
                var bracket = element.IndexOf('[');

                parsed[i] = bracket >= 0
                    ? (element.Substring(0, bracket), Convert.ToInt32(element.Substring(bracket).Replace("[", "").Replace("]", "")))
                    : (element, -1);
            }

            PathCache[propertyPath] = parsed;

            return parsed;
        }

        public static object GetTargetObjectWithProperty(SerializedProperty property)
        {
            var path = ParsePath(property.propertyPath);
            object obj = property.serializedObject.targetObject;

            for (var i = 0; i < path.Length; i++)
            {
                obj = path[i].Index >= 0
                    ? GetValue_Imp(obj, path[i].Name, path[i].Index)
                    : GetValue_Imp(obj, path[i].Name);
            }

            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            var type = source.GetType();
            var key = (type, name);

            if (!MemberCache.TryGetValue(key, out var member))
            {
                var current = type;

                while (current != null && member == null)
                {
                    member = current.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                             ?? (MemberInfo) current.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    current = current.BaseType;
                }

                MemberCache[key] = member;
            }

            switch (member)
            {
                case FieldInfo field:
                    return field.GetValue(source);
                case PropertyInfo property:
                    return property.GetValue(source, null);
            }

            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var value = GetValue_Imp(source, name);

            if (value is IList list)
            {
                return index >= 0 && index < list.Count ? list[index] : null;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return null;
            }

            IEnumerator enumerator = enumerable.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                if (!enumerator.MoveNext())
                {
                    return null;
                }
            }

            return enumerator.Current;
        }

        private static bool TryConvertNumericValue(object value, out float result)
        {
            switch (value)
            {
                case byte b:
                    result = b;
                    return true;
                case sbyte sb:
                    result = sb;
                    return true;
                case short s:
                    result = s;
                    return true;
                case ushort us:
                    result = us;
                    return true;
                case int i:
                    result = i;
                    return true;
                case uint ui:
                    result = ui;
                    return true;
                case long l:
                    result = l;
                    return true;
                case ulong ul:
                    result = ul;
                    return true;
                case float f:
                    result = f;
                    return true;
                case double d:
                    result = (float)d;
                    return true;
                case decimal m:
                    result = (float)m;
                    return true;
                default:
                    result = 0f;
                    return false;
            }
        }
    }
}
