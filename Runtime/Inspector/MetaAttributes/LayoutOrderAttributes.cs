using System;

namespace MyInspector
{
	/// <summary>Sorts the member in the inspector. Lower values are drawn first, members without it count as 0.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class PropertyOrderAttribute : MetaAttribute
	{
		public int Order { get; private set; }

		public PropertyOrderAttribute(int order)
		{
			Order = order;
		}
	}

	/// <summary>Adds vertical space before the member.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class PropertySpaceAttribute : MetaAttribute
	{
		public float Space { get; private set; }

		public PropertySpaceAttribute(float space = 8f)
		{
			Space = space;
		}
	}

	/// <summary>Customizes how a list is drawn. SerializeReference lists also get a type picker.</summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class ListDrawerSettingsAttribute : MetaAttribute
	{
		/// <summary>Shows a foldout on each element. Elements are drawn flat by default.</summary>
		public bool ShowFoldout = false;
		public bool ShowIndexLabels = false;
		public int NumberOfItemsPerPage = 0;
		public string CustomAddFunction;
		public bool HideReferenceObjectPicker = false;
	}

	/// <summary>Customizes how a dictionary is drawn.</summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class DictionaryDrawerSettingsAttribute : MetaAttribute
	{
		/// <summary>Key column width: from 0 to 1 it is a ratio of the row width, above 1 it is in pixels.</summary>
		public float KeyWidth = 125f;

		/// <summary>Number of rows per page. Above 0 it enables the pagination buttons in the header. 0 disables pagination.</summary>
		public int PageSize = 0;
	}
}
