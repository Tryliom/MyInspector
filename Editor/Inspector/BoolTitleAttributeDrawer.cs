using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BoolTitleAttribute))]
public class BoolTitleAttributeDrawer : PropertyDrawer
{
	private const float TopSpace = 8f;
	private const float BottomSpace = 3f;

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return TopSpace + EditorGUIUtility.singleLineHeight + 1f + BottomSpace;
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Boolean)
		{
			EditorGUI.PropertyField(position, property, label);

			return;
		}

		var attribute = (BoolTitleAttribute) this.attribute;
		var content = new GUIContent(attribute.Title ?? label.text, label.tooltip);
		var line = new Rect(position.x, position.y + TopSpace, position.width, EditorGUIUtility.singleLineHeight);

		EditorGUI.BeginProperty(position, content, property);
		property.boolValue = EditorGUI.ToggleLeft(line, content, property.boolValue, EditorStyles.boldLabel);
		EditorGUI.EndProperty();

		var separator = new Rect(position.x, line.yMax, position.width, 1f);

		EditorGUI.DrawRect(separator, new Color(0.5f, 0.5f, 0.5f, 0.5f));
	}
}
