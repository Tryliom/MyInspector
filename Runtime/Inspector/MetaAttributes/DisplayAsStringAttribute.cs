using System;
using UnityEngine;

namespace MyInspector
{
	/// <summary>Draws a non serialized string (or any value) as a read only text. Overflow false wraps the text.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class DisplayAsStringAttribute : MetaAttribute
	{
		public bool Overflow { get; private set; }
		public int FontSize { get; private set; }
		public TextAlignment Alignment { get; private set; }
		public bool EnableRichText { get; set; }

		public DisplayAsStringAttribute(bool overflow = true, int fontSize = 0, TextAlignment alignment = TextAlignment.Left)
		{
			Overflow = overflow;
			FontSize = fontSize;
			Alignment = alignment;
		}
	}

	/// <summary>Calls the given parameterless method once when the inspector of the object is opened.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class OnInspectorInitAttribute : MetaAttribute
	{
		public string Callback { get; private set; }

		public OnInspectorInitAttribute(string callback)
		{
			Callback = callback;
		}
	}
}
