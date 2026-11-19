using System;

namespace MyInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class FoldoutAttribute : MetaAttribute, IGroupAttribute
    {
        public string Name { get; private set; }

        /// <summary>
        /// When true, draws a minimal foldout (no header/content rectangles).
        /// </summary>
        public bool Clean { get; set; }

        public FoldoutAttribute(string name)
        {
            Name = name;
        }
    }
}
