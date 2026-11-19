using System;
using UnityEngine;

/**
 * Draws a bool as a title row: the checkbox first, then the title text (bold, with an underline).
 * Replaces the pattern [Title("X")] + a bool field labeled "X".
 */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BoolTitleAttribute : PropertyAttribute
{
	public readonly string Title;

	/** If title is null, the field name is used */
	public BoolTitleAttribute(string title = null)
	{
		Title = title;
	}
}
