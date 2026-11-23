using System;
using MyInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnumTitleAttribute))]
public class EnumTitleAttributeDrawer : PropertyDrawer
{
	private const float TopSpace = InspectorStyle.TitleTopSpace;
	private const float RowHeight = InspectorStyle.TitleRowHeight;
	private const float BottomSpace = InspectorStyle.TitleBottomSpace;

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return TopSpace + RowHeight + InspectorStyle.TitleSeparatorThickness + BottomSpace;
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Enum || fieldInfo == null)
		{
			EditorGUI.PropertyField(position, property, label);

			return;
		}

		// Manual rects ignore the indent, so apply it once and draw with a zero indent level
		var previousIndent = EditorGUI.indentLevel;

		position = EditorGUI.IndentedRect(position);
		EditorGUI.indentLevel = 0;

		DrawTitle(position, property, label);

		EditorGUI.indentLevel = previousIndent;
	}

	private void DrawTitle(Rect position, SerializedProperty property, GUIContent label)
	{
		var attribute = (EnumTitleAttribute) this.attribute;
		var content = new GUIContent(attribute.Title ?? label.text, label.tooltip);
		var row = new Rect(position.x, position.y + TopSpace, position.width, RowHeight);
		var current = (Enum) Enum.ToObject(fieldInfo.FieldType, property.intValue);

		EditorGUI.BeginProperty(position, content, property);
		EditorGUI.BeginChangeCheck();

		var result = EnumDrawerUtils.TitleRow(row, content, current);

		if (EditorGUI.EndChangeCheck())
		{
			property.intValue = Convert.ToInt32(result);
		}

		EditorGUI.EndProperty();

		EditorGUI.DrawRect(new Rect(position.x, row.yMax, position.width, InspectorStyle.TitleSeparatorThickness), InspectorStyle.TitleSeparator);
	}
}
