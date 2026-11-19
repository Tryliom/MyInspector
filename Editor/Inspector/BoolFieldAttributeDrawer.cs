using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BoolFieldAttribute))]
public class BoolFieldAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Boolean)
		{
			EditorGUI.PropertyField(position, property, label);

			return;
		}

		var attribute = (BoolFieldAttribute) this.attribute;
		var content = new GUIContent(attribute.Label ?? label.text, label.tooltip);

		EditorGUI.BeginProperty(position, content, property);
		property.boolValue = EditorGUI.ToggleLeft(position, content, property.boolValue);
		EditorGUI.EndProperty();
	}
}
