using System;

namespace MyInspector
{
	/// <summary>
	/// Draws a list as a table: one row per element, one column per member of the element.
	/// Members sharing a HorizontalGroup name are merged into one column. Works with serialized and non serialized lists.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class TableListAttribute : MetaAttribute
	{
		/// <summary>Hides the add and remove buttons and makes every cell read only.</summary>
		public bool IsReadOnly = false;
	}

	/// <summary>Sizes the table column that contains the member to the minimum width needed to display its content properly.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class TableColumnFitAttribute : MetaAttribute
	{
	}

	/// <summary>Sets the width in pixels of the table column that contains the member. Without it the column fills the free space.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class TableColumnWidthAttribute : MetaAttribute
	{
		public float Width { get; private set; }

		public TableColumnWidthAttribute(float width)
		{
			Width = width;
		}
	}
}
