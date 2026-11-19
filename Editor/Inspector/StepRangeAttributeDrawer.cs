using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StepRangeAttribute))]
public class StepRangeAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		var attribute = (StepRangeAttribute) this.attribute;

		if (property.propertyType == SerializedPropertyType.Float)
		{
			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();

			var value = EditorGUI.Slider(position, label, property.floatValue, attribute.Min, attribute.Max);

			if (EditorGUI.EndChangeCheck())
			{
				property.floatValue = Snap(value, attribute);
			}

			EditorGUI.EndProperty();
		}
		else if (property.propertyType == SerializedPropertyType.Integer)
		{
			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();

			var value = EditorGUI.IntSlider(position, label, property.intValue, Mathf.RoundToInt(attribute.Min), Mathf.RoundToInt(attribute.Max));

			if (EditorGUI.EndChangeCheck())
			{
				property.intValue = Mathf.RoundToInt(Snap(value, attribute));
			}

			EditorGUI.EndProperty();
		}
		else
		{
			EditorGUI.PropertyField(position, property, label);
		}
	}

	private static float Snap(float value, StepRangeAttribute attribute)
	{
		if (attribute.Step <= 0f)
		{
			return value;
		}

		var snapped = attribute.Min + Mathf.Round((value - attribute.Min) / attribute.Step) * attribute.Step;

		return Mathf.Clamp(snapped, attribute.Min, attribute.Max);
	}
}
