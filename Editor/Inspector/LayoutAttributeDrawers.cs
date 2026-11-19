using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(HideLabelAttribute))]
[CustomPropertyDrawer(typeof(InlinePropertyAttribute))]
public class InlinePropertyDrawer : PropertyDrawer
{
	private bool IsInline(SerializedProperty property)
	{
		return property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren && !property.isArray;
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		if (!IsInline(property))
		{
			return EditorGUI.GetPropertyHeight(property, attribute is HideLabelAttribute ? GUIContent.none : label, true);
		}

		var height = 0f;
		var child = property.Copy();
		var end = property.GetEndProperty();

		child.NextVisible(true);

		while (!SerializedProperty.EqualContents(child, end))
		{
			height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;

			if (!child.NextVisible(false))
			{
				break;
			}
		}

		return Mathf.Max(height - EditorGUIUtility.standardVerticalSpacing, EditorGUIUtility.singleLineHeight);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (!IsInline(property))
		{
			EditorGUI.PropertyField(position, property, attribute is HideLabelAttribute ? GUIContent.none : label, true);

			return;
		}

		var child = property.Copy();
		var end = property.GetEndProperty();

		child.NextVisible(true);

		while (!SerializedProperty.EqualContents(child, end))
		{
			var childHeight = EditorGUI.GetPropertyHeight(child, true);

			EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, childHeight), child, true);
			position.y += childHeight + EditorGUIUtility.standardVerticalSpacing;

			if (!child.NextVisible(false))
			{
				break;
			}
		}
	}
}

[CustomPropertyDrawer(typeof(EnumToggleButtonsAttribute))]
public class EnumToggleButtonsAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Enum)
		{
			EditorGUI.PropertyField(position, property, label);

			return;
		}

		EditorGUI.BeginProperty(position, label, property);

		var rect = EditorGUI.PrefixLabel(position, label);
		var names = property.enumDisplayNames;
		var width = rect.width / Mathf.Max(1, names.Length);
		var isFlags = fieldInfo.FieldType.IsDefined(typeof(FlagsAttribute), false) ||
		              (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericArguments().Any(t => t.IsDefined(typeof(FlagsAttribute), false)));

		for (var i = 0; i < names.Length; i++)
		{
			var buttonRect = new Rect(rect.x + width * i, rect.y, width, EditorGUIUtility.singleLineHeight);
			var style = names.Length == 1 ? EditorStyles.miniButton : i == 0 ? EditorStyles.miniButtonLeft : i == names.Length - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
			var value = isFlags ? (property.intValue & (1 << i)) != 0 : property.enumValueIndex == i;

			EditorGUI.BeginChangeCheck();
			value = GUI.Toggle(buttonRect, value, names[i], style);

			if (EditorGUI.EndChangeCheck())
			{
				if (isFlags)
				{
					property.intValue = value ? property.intValue | (1 << i) : property.intValue & ~(1 << i);
				}
				else
				{
					property.enumValueIndex = i;
				}
			}
		}

		EditorGUI.EndProperty();
	}
}
