using UnityEditor;
using UnityEngine;

namespace MyInspector.Editor
{
    /// <summary>Shared min/max slider with number fields and an optional step.</summary>
    public static class MinMaxSliderGUI
    {
        private const float Spacing = InspectorStyle.MinMaxSliderSpacing;
        private const float FieldWidth = InspectorStyle.MinMaxSliderFieldWidth;

        /// <summary>Draws [min field][slider][max field] in the rect and returns true when a value changed.</summary>
        public static bool Draw(Rect rect, ref float min, ref float max, float limitMin, float limitMax, float step, bool isInt)
        {
            if (isInt)
            {
                step = Mathf.Max(1f, Mathf.Round(step));
            }

            var indent = EditorGUI.indentLevel;

            EditorGUI.indentLevel = 0;

            var minRect = new Rect(rect.x, rect.y, FieldWidth, rect.height);
            var maxRect = new Rect(rect.xMax - FieldWidth, rect.y, FieldWidth, rect.height);
            var sliderRect = new Rect(minRect.xMax + Spacing, rect.y, Mathf.Max(0f, maxRect.x - minRect.xMax - Spacing * 2f), rect.height);

            var low = Snap(min, limitMin, limitMax, step, isInt);
            var high = Snap(max, limitMin, limitMax, step, isInt);

            EditorGUI.BeginChangeCheck();

            low = DrawNumber(minRect, low, isInt);
            high = DrawNumber(maxRect, high, isInt);
            if (sliderRect.width >= 40f)
            {
                EditorGUI.MinMaxSlider(sliderRect, ref low, ref high, limitMin, limitMax);
            }

            var changed = EditorGUI.EndChangeCheck();

            EditorGUI.indentLevel = indent;

            if (changed)
            {
                low = Snap(low, limitMin, limitMax, step, isInt);
                high = Snap(high, limitMin, limitMax, step, isInt);

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

        // Delayed fields commit on Enter/focus loss, so the min/max crossing check doesn't run while typing
        private static float DrawNumber(Rect rect, float value, bool isInt)
        {
            return isInt ? EditorGUI.DelayedIntField(rect, Mathf.RoundToInt(value)) : EditorGUI.DelayedFloatField(rect, value);
        }
    }
}
