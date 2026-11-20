using MyInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BoolTitleAttribute))]
public class BoolTitleAttributeDrawer : PropertyDrawer
{
	private const float TopSpace = InspectorStyle.TitleTopSpace;
	private const float BottomSpace = InspectorStyle.TitleBottomSpace;

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return TopSpace + EditorGUIUtility.singleLineHeight + InspectorStyle.TitleSeparatorThickness + BottomSpace;
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

		var separator = new Rect(position.x, line.yMax, 		position.width, InspectorStyle.TitleSeparatorThickness);

				EditorGUI.DrawRect(separator, InspectorStyle.TitleSeparator);
	}
}
