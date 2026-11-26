using System;
using UnityEditor;
using UnityEngine;

public static class EnumDrawerUtils
{
	public static Enum Popup(Rect rect, Enum value)
	{
		var type = value.GetType();

		if (type.IsDefined(typeof(FlagsAttribute), false))
		{
			return EditorGUI.EnumFlagsField(rect, value);
		}

		return EditorGUI.EnumPopup(rect, value);
	}

	/// <summary>Draws "[bold title] [popup]" in the given rect and returns the new value.</summary>
	public static Enum TitleRow(Rect rect, GUIContent content, Enum value)
	{
		return Popup(TitleLabel(rect, content), value);
	}

	/// <summary>Draws the bold title of a title row and returns the remaining rect, meant for the value control.</summary>
	public static Rect TitleLabel(Rect rect, GUIContent content)
	{
		var labelWidth = Mathf.Min(EditorStyles.boldLabel.CalcSize(content).x + 6f, rect.width * 0.5f);
		var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);

		EditorGUI.LabelField(labelRect, content, EditorStyles.boldLabel);

		return new Rect(rect.x + labelWidth, rect.y + 1f, rect.width - labelWidth, rect.height - 2f);
	}
}
