using System;
using System.Collections.Generic;
using System.Reflection;

namespace MyInspector.Editor
{
    /// <summary>
    /// Caches custom attribute lookups: MemberInfo.GetCustomAttribute instantiates new attribute objects on every call,
    /// which is too costly to run for every field on every IMGUI event. Attributes are immutable, so the cache is safe
    /// and is reset by the domain reload.
    /// </summary>
    public static class AttributeCache
    {
        private static readonly Dictionary<(MemberInfo Member, Type Attribute), Attribute[]> Cache = new();

        public static T Get<T>(MemberInfo member) where T : Attribute
        {
            var all = GetAll<T>(member);

            return all.Length > 0 ? (T)all[0] : null;
        }

        public static Attribute[] GetAll<T>(MemberInfo member) where T : Attribute
        {
            if (member == null)
            {
                return Array.Empty<Attribute>();
            }

            var key = (member, typeof(T));

            if (!Cache.TryGetValue(key, out var cached))
            {
                cached = (Attribute[])Attribute.GetCustomAttributes(member, typeof(T), true);
                Cache[key] = cached;
            }

            return cached;
        }
    }
}
