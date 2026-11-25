using System;

namespace MyInspector
{
    /// <summary>
    /// Class level attribute: the header of the foldout drawn for an instance of this class shows the string
    /// returned by the given method or property (parameterless, may contain several lines).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class FoldoutLabelAttribute : Attribute
    {
        public string MemberName { get; private set; }

        public FoldoutLabelAttribute(string memberName)
        {
            MemberName = memberName;
        }
    }
}
