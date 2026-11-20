using MyInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NumberConditionRange))]
[CustomPropertyDrawer(typeof(NumberRangeSettingsAttribute))]
public class NumberConditionRangeDrawer : PropertyDrawer
{
	private const float Spacing = InspectorStyle.RangeSpacing;
	private const float TypeWidth = InspectorStyle.RangeTypeWidth;
	private const float ConditionWidth = InspectorStyle.RangeConditionWidth;
	private const float FieldWidth = InspectorStyle.RangeFieldWidth;

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		var settings = attribute as NumberRangeSettingsAttribute;

		// The attribute drawer takes precedence over type drawers, so NumberRange fields are routed here
		if (property.type == nameof(NumberRange))
		{
			NumberRangeDrawer.Draw(position, property, label, settings);

			return;
		}

		var typeProp = property.FindPropertyRelative(nameof(NumberConditionRange.Type));
		var conditionProp = property.FindPropertyRelative(nameof(NumberConditionRange.Condition));
		var valueProp = property.FindPropertyRelative(nameof(NumberConditionRange.Value));
		var maxProp = property.FindPropertyRelative(nameof(NumberConditionRange.MaxValue));

		EditorGUI.BeginProperty(position, label, property);

		var indent = EditorGUI.indentLevel;
		// Caps the label so the controls keep enough room in narrow inspectors
		var labelWidth = Mathf.Min(EditorGUIUtility.labelWidth, position.width * 0.3f);
		var labelRect = new Rect(position.x, position.y, labelWidth, position.height);

		EditorGUI.LabelField(labelRect, label);
		position = new Rect(position.x + labelWidth, position.y, position.width - labelWidth, position.height);
		EditorGUI.indentLevel = 0;

		var typeForced = settings != null && settings.HasType;
		var typeRect = new Rect(position.x, position.y, typeForced ? 0f : TypeWidth, position.height);
		var conditionX = typeForced ? position.x : typeRect.xMax + Spacing;
		var conditionRect = new Rect(conditionX, position.y, ConditionWidth, position.height);
		var rest = new Rect(conditionRect.xMax + Spacing, position.y, position.xMax - conditionRect.xMax - Spacing, position.height);

		if (typeForced)
		{
			if (typeProp.enumValueIndex != (int) settings.Type)
			{
				typeProp.enumValueIndex = (int) settings.Type;
			}
		}
		else
		{
			EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
		}

		EditorGUI.PropertyField(conditionRect, conditionProp, GUIContent.none);

		var isInt = (NumberType) typeProp.enumValueIndex == NumberType.Int;
		var condition = (RangeCondition) conditionProp.enumValueIndex;
		var hasLimits = settings != null && settings.Max > settings.Min;
		var step = settings?.Step ?? 0f;

		if (isInt)
		{
			step = Mathf.Max(1f, Mathf.Round(step));
		}

		float Snap(float v)
		{
			return MinMaxSliderGUI.Snap(v, settings.Min, settings.Max, step, isInt);
		}

		float Clamp(float v)
		{
			return MinMaxSliderGUI.Clamp(v, settings.Min, settings.Max, isInt);
		}

		EditorGUI.BeginChangeCheck();

		var value = valueProp.floatValue;
		var maxValue = maxProp.floatValue;

		// Only clamped: values typed between two steps must stay as typed
		if (hasLimits)
		{
			value = Clamp(value);
			maxValue = Clamp(maxValue);
		}

		if (condition == RangeCondition.Between)
		{
			if (hasLimits)
			{
				MinMaxSliderGUI.Draw(rest, ref value, ref maxValue, settings.Min, settings.Max, step, isInt);
			}
			else
			{
				var half = (rest.width - Spacing) * 0.5f;
				value = DrawNumber(new Rect(rest.x, rest.y, half, rest.height), value, isInt);
				maxValue = DrawNumber(new Rect(rest.x + half + Spacing, rest.y, half, rest.height), maxValue, isInt);

				if (maxValue < value)
				{
					maxValue = value;
				}
			}
		}
		else if (hasLimits)
		{
			var fieldRect = new Rect(rest.xMax - FieldWidth, rest.y, FieldWidth, rest.height);
			var sliderRect = new Rect(rest.x, rest.y, fieldRect.x - rest.x - Spacing, rest.height);

			EditorGUI.BeginChangeCheck();

			var slid = GUI.HorizontalSlider(sliderRect, value, settings.Min, settings.Max);

			if (EditorGUI.EndChangeCheck())
			{
				value = Snap(slid);
			}

			value = Clamp(DrawNumber(fieldRect, value, isInt));
		}
		else
		{
			value = DrawNumber(rest, value, isInt);
		}

		if (EditorGUI.EndChangeCheck())
		{
			valueProp.floatValue = hasLimits ? Clamp(value) : value;
			maxProp.floatValue = hasLimits ? Clamp(maxValue) : maxValue;
		}
		else if (!Mathf.Approximately(valueProp.floatValue, value) || !Mathf.Approximately(maxProp.floatValue, maxValue))
		{
			// Keeps stored data consistent when the type or the attribute settings change
			valueProp.floatValue = value;
			maxProp.floatValue = maxValue;
		}

		EditorGUI.indentLevel = indent;
		EditorGUI.EndProperty();
	}

	private static float DrawNumber(Rect rect, float value, bool isInt)
	{
		return isInt ? EditorGUI.IntField(rect, Mathf.RoundToInt(value)) : EditorGUI.FloatField(rect, value);
	}
}
