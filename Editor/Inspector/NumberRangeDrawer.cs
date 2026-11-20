using MyInspector.Editor;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NumberRange))]
public class NumberRangeDrawer : PropertyDrawer
{
	private const float Spacing = 4f;
	private const float TypeWidth = 50f;
	private const float ModeWidth = 60f;
	private const float FieldWidth = 50f;
	private const float StepButtonWidth = 20f;

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		Draw(position, property, label, fieldInfo?.GetCustomAttribute<NumberRangeSettingsAttribute>());
	}

	/// <summary>Shared with NumberConditionRangeDrawer, which owns the NumberRangeSettings attribute drawer.</summary>
	public static void Draw(Rect position, SerializedProperty property, GUIContent label, NumberRangeSettingsAttribute settings)
	{
		var typeProp = property.FindPropertyRelative(nameof(NumberRange.Type));
		var modeProp = property.FindPropertyRelative(nameof(NumberRange.Mode));
		var valueProp = property.FindPropertyRelative(nameof(NumberRange.Value));
		var maxProp = property.FindPropertyRelative(nameof(NumberRange.MaxValue));

		EditorGUI.BeginProperty(position, label, property);

		var indent = EditorGUI.indentLevel;
		var labelWidth = EditorGUIUtility.labelWidth;
		var labelRect = new Rect(position.x, position.y, labelWidth, position.height);

		EditorGUI.LabelField(labelRect, label);
		// Unity starts the field after the label plus a 2px prefix padding
		const float prefixPadding = 2f;

		position = new Rect(position.x + labelWidth + prefixPadding, position.y, position.width - labelWidth - prefixPadding, position.height);
		EditorGUI.indentLevel = 0;

		var typeForced = settings != null && settings.HasType;
		var typeRect = new Rect(position.x, position.y, typeForced ? 0f : TypeWidth, position.height);
		var modeX = typeForced ? position.x : typeRect.xMax + Spacing;
		var modeRect = new Rect(modeX, position.y, ModeWidth, position.height);
		var rest = new Rect(modeRect.xMax + Spacing, position.y, position.xMax - modeRect.xMax - Spacing, position.height);

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

		EditorGUI.PropertyField(modeRect, modeProp, GUIContent.none);

		var isInt = (NumberType) typeProp.enumValueIndex == NumberType.Int;
		var isRange = (NumberRangeMode) modeProp.enumValueIndex == NumberRangeMode.Range;
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

		if (hasLimits)
		{
			var leftRect = new Rect(rest.x, rest.y, StepButtonWidth, rest.height);
			var rightRect = new Rect(rest.xMax - StepButtonWidth, rest.y, StepButtonWidth, rest.height);

			rest = new Rect(leftRect.xMax + Spacing, rest.y, rightRect.x - leftRect.xMax - Spacing * 2f, rest.height);

			var direction = GUI.Button(leftRect, "<") ? -1f : GUI.Button(rightRect, ">") ? 1f : 0f;

			if (direction != 0f)
			{
				// Moves both bounds together without squeezing the range against a limit
				var delta = direction * step;
				var low = isRange ? Mathf.Min(value, maxValue) : value;
				var high = isRange ? Mathf.Max(value, maxValue) : value;

				delta = Mathf.Clamp(delta, settings.Min - low, settings.Max - high);
				value += delta;
				maxValue += delta;
				GUI.changed = true;
			}
		}

		if (isRange)
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
			maxValue = value;
		}
		else
		{
			value = DrawNumber(rest, value, isInt);
			maxValue = value;
		}

		if (EditorGUI.EndChangeCheck())
		{
			valueProp.floatValue = hasLimits ? Clamp(value) : value;
			maxProp.floatValue = hasLimits ? Clamp(maxValue) : maxValue;
		}
		else if (!Mathf.Approximately(valueProp.floatValue, value) || !Mathf.Approximately(maxProp.floatValue, maxValue))
		{
			// Keeps stored data consistent when the type, the mode or the attribute settings change
			valueProp.floatValue = value;
			maxProp.floatValue = maxValue;
		}

		EditorGUI.indentLevel = indent;
		EditorGUI.EndProperty();
	}

	private static float DrawNumber(Rect rect, float value, bool isInt)
	{
		// Delayed so the min/max crossing check runs on commit, not on every keystroke
		return isInt ? EditorGUI.DelayedIntField(rect, Mathf.RoundToInt(value)) : EditorGUI.DelayedFloatField(rect, value);
	}
}
