using System;
using UnityEngine;

/** Hides the label of the field. On a serializable class, its children are drawn inline without foldout. */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class HideLabelAttribute : PropertyAttribute
{
}

/** Draws the children of a serializable class inline, without foldout. */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class InlinePropertyAttribute : PropertyAttribute
{
}

/** Draws an enum (or flags enum) as a row of toggle buttons. */
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class EnumToggleButtonsAttribute : PropertyAttribute
{
}
