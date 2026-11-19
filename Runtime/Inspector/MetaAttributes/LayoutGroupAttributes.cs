using System;

namespace MyInspector
{
	/// <summary>Draws the member inside a tab. Consecutive members with the same group form one tab bar.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class TabAttribute : MetaAttribute, IGroupAttribute
	{
		public string Group { get; private set; }
		public string Name { get; private set; }

		public TabAttribute(string group, string name)
		{
			Group = group;
			Name = name;
		}

		public TabAttribute(string name) : this("_default", name)
		{
		}
	}

	/// <summary>Draws consecutive members of the same group side by side. Width below 1 is a fraction, 1 or more is pixels.</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class HorizontalGroupAttribute : MetaAttribute, IGroupAttribute
	{
		public string Name { get; private set; }
		public float Width { get; private set; }

		public HorizontalGroupAttribute(string name, float width = 0f)
		{
			Name = name;
			Width = width;
		}
	}

	/// <summary>Stacks consecutive members of the same group vertically (useful as a column inside a HorizontalGroup).</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class VerticalGroupAttribute : MetaAttribute, IGroupAttribute
	{
		public string Name { get; private set; }

		public VerticalGroupAttribute(string name)
		{
			Name = name;
		}
	}
}
