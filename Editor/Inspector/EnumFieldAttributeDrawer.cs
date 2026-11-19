using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnumFieldAttribute))]
public class EnumFieldAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Enum || fieldInfo == null)
		{
			EditorGUI.PropertyField(position, property, label);

			return;
		}

		var attribute = (EnumFieldAttribute) this.attribute;
		var content = new GUIContent(attribute.Label ?? label.text, label.tooltip);
		var current = (Enum) Enum.ToObject(fieldInfo.FieldType, property.intValue);

		EditorGUI.BeginProperty(position, content, property);
		EditorGUI.BeginChangeCheck();

		var rect = EditorGUI.PrefixLabel(position, content);
		var result = EnumDrawerUtils.Popup(rect, current);

		if (EditorGUI.EndChangeCheck())
		{
			property.intValue = Convert.ToInt32(result);
		}

		EditorGUI.EndProperty();
	}
}
