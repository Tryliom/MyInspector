using System;

namespace MyInspector
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class InputAxisAttribute : DrawerAttribute
    {
    }
}
