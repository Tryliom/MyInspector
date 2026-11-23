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

			var fieldRect = EditorGUI.PrefixLabel(position, label);
			var fieldWidth = 50f;
			var sliderRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - fieldWidth - 4f, fieldRect.height);
			var numberRect = new Rect(fieldRect.xMax - fieldWidth, fieldRect.y, fieldWidth, fieldRect.height);
			var current = property.floatValue;

			var sliderValue = GUI.HorizontalSlider(sliderRect, Mathf.Clamp(current, attribute.Min, attribute.Max), attribute.Min, attribute.Max);

			if (!Mathf.Approximately(sliderValue, Mathf.Clamp(current, attribute.Min, attribute.Max)))
			{
				property.floatValue = Snap(sliderValue, attribute);
			}

			// The number field accepts any value, even one outside the step.
			var typed = EditorGUI.FloatField(numberRect, property.floatValue);

			if (!Mathf.Approximately(typed, property.floatValue))
			{
				property.floatValue = Mathf.Clamp(typed, attribute.Min, attribute.Max);
			}

			EditorGUI.EndChangeCheck();
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
