using System;
using UnityEngine;
using Random = UnityEngine.Random;

public enum NumberRangeMode
{
	Exact,
	Range
}

/// <summary>
/// A number that is either an exact value or a range to pick a random value from.
/// Slider bounds and step can be customized with [NumberRangeSettings(...)].
/// </summary>
[Serializable]
public class NumberRange
{
	public NumberType Type = NumberType.Float;
	public NumberRangeMode Mode = NumberRangeMode.Exact;
	public float Value;
	public float MaxValue;

	public NumberRange()
	{
	}

	public NumberRange(float exact, NumberType type = NumberType.Float)
	{
		Type = type;
		Set(exact);
	}

	public NumberRange(float min, float max, NumberType type = NumberType.Float)
	{
		Type = type;
		Set(min, max);
	}

	public bool IsRange => Mode == NumberRangeMode.Range;

	/// <summary>
	/// Lowest possible value
	/// </summary>
	public float Min => Mode == NumberRangeMode.Exact ? Value : Math.Min(Value, MaxValue);

	/// <summary>
	/// Highest possible value
	/// </summary>
	public float Max => Mode == NumberRangeMode.Exact ? Value : Math.Max(Value, MaxValue);

	public int MinInt => Mathf.RoundToInt(Min);

	public int MaxInt => Mathf.RoundToInt(Max);

	public void Set(float exact)
	{
		Mode = NumberRangeMode.Exact;
		Value = exact;
		MaxValue = exact;
	}

	/// <summary>
	/// Becomes an exact value when both bounds are equal
	/// </summary>
	public void Set(float min, float max)
	{
		if (Mathf.Approximately(min, max))
		{
			Set(min);
			return;
		}

		Mode = NumberRangeMode.Range;
		Value = Math.Min(min, max);
		MaxValue = Math.Max(min, max);
	}

	/// <summary>
	/// Random value in the range (or the exact value). With the Int type, both bounds are inclusive.
	/// </summary>
	public float GetRandomFloat()
	{
		if (Type == NumberType.Int)
		{
			return GetRandomInt();
		}

		return Mode == NumberRangeMode.Exact || Mathf.Approximately(Min, Max) ? Min : Random.Range(Min, Max);
	}

	/// <summary>
	/// Random rounded value in the range (or the exact value), both bounds are inclusive.
	/// </summary>
	public int GetRandomInt()
	{
		var min = MinInt;
		var max = MaxInt;

		return min >= max ? min : Random.Range(min, max + 1);
	}

#if UNITY_EDITOR
	/// <summary>
	/// Readable value like "2" or "1-3"
	/// </summary>
	public string GetDescription(string format = "0.##")
	{
		if (Mode == NumberRangeMode.Exact || Mathf.Approximately(Min, Max))
		{
			return Min.ToString(format);
		}

		return $"{Min.ToString(format)}-{Max.ToString(format)}";
	}

	public bool IsZero => Mathf.Approximately(Min, 0f) && Mathf.Approximately(Max, 0f);
#endif
}
