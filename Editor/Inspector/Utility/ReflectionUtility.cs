using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MyInspector.Editor
{
    public static class ReflectionUtility
    {
        public static IEnumerable<FieldInfo> GetAllFields(object target, Func<FieldInfo, bool> predicate)
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");
                yield break;
            }

            List<Type> types = GetSelfAndBaseTypes(target);

            for (int i = types.Count - 1; i >= 0; i--)
            {
                IEnumerable<FieldInfo> fieldInfos = types[i]
                    .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(predicate);

                foreach (var fieldInfo in fieldInfos)
                {
                    yield return fieldInfo;
                }
            }
        }

        public static IEnumerable<PropertyInfo> GetAllProperties(object target, Func<PropertyInfo, bool> predicate)
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");
                yield break;
            }

            List<Type> types = GetSelfAndBaseTypes(target);

            for (int i = types.Count - 1; i >= 0; i--)
            {
                IEnumerable<PropertyInfo> propertyInfos = types[i]
                    .GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(predicate);

                foreach (var propertyInfo in propertyInfos)
                {
                    yield return propertyInfo;
                }
            }
        }

        public static IEnumerable<MethodInfo> GetAllMethods(object target, Func<MethodInfo, bool> predicate)
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");
                yield break;
            }

            List<Type> types = GetSelfAndBaseTypes(target);

            for (int i = types.Count - 1; i >= 0; i--)
            {
                IEnumerable<MethodInfo> methodInfos = types[i]
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(predicate);

                foreach (var methodInfo in methodInfos)
                {
                    yield return methodInfo;
                }
            }
        }

        private const BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<(Type, string), FieldInfo> FieldCache = new Dictionary<(Type, string), FieldInfo>();
        private static readonly Dictionary<(Type, string), PropertyInfo> PropertyCache = new Dictionary<(Type, string), PropertyInfo>();
        private static readonly Dictionary<(Type, string), MethodInfo> MethodCache = new Dictionary<(Type, string), MethodInfo>();

        // Lookups keep the historical order: the most base type is searched first
        public static FieldInfo GetField(object target, string fieldName)
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");

                return null;
            }

            var key = (target.GetType(), fieldName);

            if (FieldCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            FieldInfo result = null;
            var types = GetSelfAndBaseTypes(target);

            for (var i = types.Count - 1; i >= 0 && result == null; i--)
            {
                foreach (var field in types[i].GetFields(AllMembers))
                {
                    if (field.Name.Equals(fieldName, StringComparison.Ordinal))
                    {
                        result = field;

                        break;
                    }
                }
            }

            FieldCache[key] = result;

            return result;
        }

        public static PropertyInfo GetProperty(object target, string propertyName)
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");

                return null;
            }

            var key = (target.GetType(), propertyName);

            if (PropertyCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            PropertyInfo result = null;
            var types = GetSelfAndBaseTypes(target);

            for (var i = types.Count - 1; i >= 0 && result == null; i--)
            {
                foreach (var property in types[i].GetProperties(AllMembers))
                {
                    if (property.Name.Equals(propertyName, StringComparison.Ordinal))
                    {
                        result = property;

                        break;
                    }
                }
            }

            PropertyCache[key] = result;

            return result;
        }

        public static MethodInfo GetMethod(object target, string methodName)
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");

                return null;
            }

            var key = (target.GetType(), methodName);

            if (MethodCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            MethodInfo result = null;
            var types = GetSelfAndBaseTypes(target);

            for (var i = types.Count - 1; i >= 0 && result == null; i--)
            {
                foreach (var method in types[i].GetMethods(AllMembers))
                {
                    if (method.Name.Equals(methodName, StringComparison.Ordinal))
                    {
                        result = method;

                        break;
                    }
                }
            }

            MethodCache[key] = result;

            return result;
        }

        public static Type GetListElementType(Type listType)
        {
            if (listType.IsGenericType)
            {
                return listType.GetGenericArguments()[0];
            }
            else
            {
                return listType.GetElementType();
            }
        }

        /// <summary>
        ///		Get type and all base types of target, sorted as following:
        ///		<para />[target's type, base type, base's base type, ...]
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private static List<Type> GetSelfAndBaseTypes(object target)
        {
            List<Type> types = new List<Type>()
            {
                target.GetType()
            };

            while (types.Last().BaseType != null)
            {
                types.Add(types.Last().BaseType);
            }

            return types;
        }
    }
}
