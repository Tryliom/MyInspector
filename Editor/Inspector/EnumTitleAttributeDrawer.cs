using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnumTitleAttribute))]
public class EnumTitleAttributeDrawer : PropertyDrawer
{
	private const float TopSpace = 8f;
	private const float RowHeight = 20f;
	private const float BottomSpace = 3f;

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return TopSpace + RowHeight + 1f + BottomSpace;
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Enum || fieldInfo == null)
		{
			EditorGUI.PropertyField(position, property, label);

			return;
		}

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

		EditorGUI.DrawRect(new Rect(position.x, row.yMax, position.width, 1f), new Color(0.5f, 0.5f, 0.5f, 0.5f));
	}
}
