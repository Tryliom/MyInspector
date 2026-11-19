using System;
using UnityEngine;

/**
 * Range slider for int and float fields that snaps to a step.
 * Usage: [StepRange(0, 1f, 0.05f)] on a float, [StepRange(0, 100, 5)] on an int.
 */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class StepRangeAttribute : PropertyAttribute
{
	public readonly float Min;
	public readonly float Max;
	public readonly float Step;

	public StepRangeAttribute(float min, float max, float step)
	{
		Min = min;
		Max = max;
		Step = step;
	}

	public StepRangeAttribute(int min, int max, int step) : this((float) min, max, step)
	{
	}
}
