using System;
using UnityEngine;

/**
 * Draws a bool with the checkbox first, then the label (normal weight, no separator or extra space).
 * Simpler version of BoolTitle, meant for bools that are not section headers.
 */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BoolFieldAttribute : PropertyAttribute
{
	public readonly string Label;

	/** If label is null, the field name is used */
	public BoolFieldAttribute(string label = null)
	{
		Label = label;
	}
}
