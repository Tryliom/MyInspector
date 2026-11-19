using System;
using UnityEngine;

public enum NumberType
{
	Int,
	Float
}

public enum RangeCondition
{
	MoreThan,
	LessThan,
	Between
}

/// <summary>
/// Optional settings for a NumberRange or NumberConditionRange field: slider bounds and increment step.
/// Usage: [NumberRangeSettings(0, 100, 5)]
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class NumberRangeSettingsAttribute : PropertyAttribute
{
	public readonly float Min;
	public readonly float Max;
	public readonly float Step;
	public readonly bool HasType;
	public readonly NumberType Type;

	public NumberRangeSettingsAttribute(float min, float max, float step = 0f)
	{
		Min = min;
		Max = max;
		Step = step;
	}

	/// <summary>
	/// Forces the number type; the type popup is then hidden in the inspector.
	/// </summary>
	public NumberRangeSettingsAttribute(NumberType type, float min, float max, float step = 0f) : this(min, max, step)
	{
		HasType = true;
		Type = type;
	}
}

[Serializable]
public class NumberConditionRange
{
	public NumberType Type = NumberType.Int;
	public RangeCondition Condition = RangeCondition.MoreThan;
	public float Value;
	public float MaxValue;

	/// <summary>
	/// Value is the threshold for MoreThan/LessThan, and the lower bound for Between (MaxValue is the upper bound).
	/// Comparisons are inclusive.
	/// </summary>
	public bool IsValid(float number)
	{
		switch (Condition)
		{
			case RangeCondition.MoreThan:
				return number >= Value;
			case RangeCondition.LessThan:
				return number <= Value;
			default:
				return number >= Math.Min(Value, MaxValue) && number <= Math.Max(Value, MaxValue);
		}
	}

	public bool IsValid(int number)
	{
		return IsValid((float) number);
	}

#if UNITY_EDITOR
	/// <summary>
	/// Readable text
	/// </summary>
	public string GetDescription(string label, bool asPercent = false)
	{
		var suffix = asPercent ? "%" : "";
		var factor = asPercent ? 100f : 1f;
		var value = Value * factor;
		var maxValue = MaxValue * factor;

		switch (Condition)
		{
			case RangeCondition.MoreThan:
				return $"{label} >= {value:0.##}{suffix}";
			case RangeCondition.LessThan:
				return $"{label} <= {value:0.##}{suffix}";
			default:
				return $"{label} {Math.Min(value, maxValue):0.##}-{Math.Max(value, maxValue):0.##}{suffix}";
		}
	}
#endif
}