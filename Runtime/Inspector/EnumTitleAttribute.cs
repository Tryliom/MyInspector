using System;
using UnityEngine;

/**
 * Draws an enum as a title row: the title text (bold) followed by the enum popup, with an underline.
 * When the enum is the first field of a FoldoutGroup, the group header shows the arrow, the title and the popup.
 */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class EnumTitleAttribute : PropertyAttribute
{
	public readonly string Title;

	/** If title is null, the field name is used */
	public EnumTitleAttribute(string title = null)
	{
		Title = title;
	}
}
