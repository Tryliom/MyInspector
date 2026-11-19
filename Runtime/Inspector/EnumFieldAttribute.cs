using System;
using UnityEngine;

/**
 * Draws an enum popup on a single compact row with a custom label (normal weight, no separator or extra space).
 * Simpler version of EnumTitle, meant for enums that are not section headers.
 */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class EnumFieldAttribute : PropertyAttribute
{
	public readonly string Label;

	/** If label is null, the field name is used */
	public EnumFieldAttribute(string label = null)
	{
		Label = label;
	}
}
