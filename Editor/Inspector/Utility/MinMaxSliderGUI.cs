using UnityEditor;
using UnityEngine;

namespace MyInspector.Editor
{
    /// <summary>Shared min/max slider with number fields and an optional step.</summary>
    public static class MinMaxSliderGUI
    {
        private const float Spacing = InspectorStyle.MinMaxSliderSpacing;
        private const float FieldWidth = InspectorStyle.MinMaxSliderFieldWidth;

        private static readonly GUIContent MeasureContent = new GUIContent();

        /// <summary>Width needed to display numbers within [low, high]; a minus sign is only counted when low is negative.</summary>
        public static float FitWidth(float low, float high, bool isInt)
        {
            var magnitude = Mathf.Max(Mathf.Abs(low), Mathf.Abs(high));
            var text = isInt ? Mathf.RoundToInt(magnitude).ToString() : magnitude.ToString("0.##");

            MeasureContent.text = low < 0f ? "-" + text : text;

            var width = EditorStyles.numberField.CalcSize(MeasureContent).x + InspectorStyle.RangeFieldPadding;

            return Mathf.Max(InspectorStyle.RangeFieldMinWidth, width);
        }

        /// <summary>Draws [min field][slider][max field] in the rect and returns true when a value changed.</summary>
        public static bool Draw(Rect rect, ref float min, ref float max, float limitMin, float limitMax, float step, bool isInt)
        {
            if (isInt)
            {
                step = Mathf.Max(1f, Mathf.Round(step));
            }

            var indent = EditorGUI.indentLevel;

            EditorGUI.indentLevel = 0;

            var fieldWidth = Mathf.Min(FieldWidth, FitWidth(limitMin, limitMax, isInt));
            var minRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            var maxRect = new Rect(rect.xMax - fieldWidth, rect.y, fieldWidth, rect.height);
            var sliderRect = new Rect(minRect.xMax + Spacing, rect.y, Mathf.Max(0f, maxRect.x - minRect.xMax - Spacing * 2f), rect.height);

            var low = Clamp(min, limitMin, limitMax, isInt);
            var high = Clamp(max, limitMin, limitMax, isInt);

            EditorGUI.BeginChangeCheck();

            low = DrawNumber(minRect, low, isInt);
            high = DrawNumber(maxRect, high, isInt);

            var changed = EditorGUI.EndChangeCheck();

            // Typed values are kept as is (only clamped); the step applies to the slider only
            low = Clamp(low, limitMin, limitMax, isInt);
            high = Clamp(high, limitMin, limitMax, isInt);

            if (sliderRect.width >= 40f)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.MinMaxSlider(sliderRect, ref low, ref high, limitMin, limitMax);

                if (EditorGUI.EndChangeCheck())
                {
                    changed = true;
                    low = Snap(low, limitMin, limitMax, step, isInt);
                    high = Snap(high, limitMin, limitMax, step, isInt);
                }
            }

            EditorGUI.indentLevel = indent;

            if (changed)
            {

                if (low > high)
                {
                    // Keeps the edited field authoritative when the two cross
                    if (!Mathf.Approximately(low, min))
                    {
                        high = low;
                    }
                    else
                    {
                        low = high;
                    }
                }
            }

            var result = !Mathf.Approximately(low, min) || !Mathf.Approximately(high, max);

            min = low;
            max = high;

            return result;
        }

        public static float Snap(float value, float limitMin, float limitMax, float step, bool isInt)
        {
            if (step > 0f)
            {
                value = limitMin + Mathf.Round((value - limitMin) / step) * step;
            }

            value = Mathf.Clamp(value, limitMin, limitMax);

            return isInt ? Mathf.Round(value) : value;
        }

        public static float Clamp(float value, float limitMin, float limitMax, bool isInt)
        {
            value = Mathf.Clamp(value, limitMin, limitMax);

            return isInt ? Mathf.Round(value) : value;
        }

        // Delayed fields commit on Enter/focus loss, so the min/max crossing check doesn't run while typing
        private static float DrawNumber(Rect rect, float value, bool isInt)
        {
            return isInt ? EditorGUI.DelayedIntField(rect, Mathf.RoundToInt(value)) : EditorGUI.DelayedFloatField(rect, value);
        }
    }
}
