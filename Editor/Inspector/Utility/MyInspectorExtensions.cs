using System;
using UnityEditor;

namespace MyInspector.Editor
{
    /// <summary>
    /// Extension points that let a project customize the inspector without modifying the package.
    /// </summary>
    public static class MyInspectorExtensions
    {
        /// <summary>
        /// Raised when the list/dictionary drawers build their context menu.
        /// Arguments: the menu and the property path of the targeted element (or of the whole collection).
        /// </summary>
        public static event Action<GenericMenu, string> CollectionContextMenu;

        internal static void InvokeCollectionContextMenu(GenericMenu menu, string path)
        {
            CollectionContextMenu?.Invoke(menu, path);
        }
    }
}
