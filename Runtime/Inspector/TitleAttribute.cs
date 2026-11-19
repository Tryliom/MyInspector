using System;
using UnityEngine;

/**
 * Draws a bold title with an underline above the decorated field.
 */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public class TitleAttribute : PropertyAttribute
{
	public readonly string Title;

	public TitleAttribute(string title)
	{
		Title = title;
	}
}
