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
		var labelWidth = Mathf.Min(EditorStyles.boldLabel.CalcSize(content).x + 6f, rect.width * 0.5f);
		var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
		var popupRect = new Rect(rect.x + labelWidth, rect.y + 1f, rect.width - labelWidth, rect.height - 2f);

		EditorGUI.LabelField(labelRect, content, EditorStyles.boldLabel);

		return Popup(popupRect, value);
	}
}
