using System;
using System.Linq;
using MyInspector;
using MyInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(HideLabelAttribute))]
[CustomPropertyDrawer(typeof(InlinePropertyAttribute))]
public class InlinePropertyDrawer : PropertyDrawer
{
	private bool IsInline(SerializedProperty property)
	{
		return property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren && !property.isArray;
	}

	private static readonly System.Collections.Generic.Dictionary<Type, System.Collections.Generic.List<System.Reflection.MethodInfo>> ButtonCache = new();

	// [Button] methods declared on the inlined type are drawn after its fields
	internal static System.Collections.Generic.List<System.Reflection.MethodInfo> GetButtons(Type type)
	{
		if (ButtonCache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		var buttons = new System.Collections.Generic.List<System.Reflection.MethodInfo>();
		const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

		foreach (var method in type.GetMethods(flags))
		{
			if (method.GetCustomAttributes(typeof(ButtonAttribute), true).Length == 0)
			{
				continue;
			}

			if (ButtonParameterDrawer.IsInvokable(method))
			{
				buttons.Add(method);
			}
		}

		ButtonCache[type] = buttons;

		return buttons;
	}

	private static System.Collections.Generic.List<System.Reflection.MethodInfo> GetButtons(SerializedProperty property)
	{
		var target = PropertyUtility.GetTargetObjectOfProperty(property);

		return target == null ? null : GetButtons(target.GetType());
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		if (!IsInline(property))
		{
			return EditorGUI.GetPropertyHeight(property, attribute is HideLabelAttribute ? GUIContent.none : label, true);
		}

		var height = 0f;
		var child = property.Copy();
		var end = property.GetEndProperty();

		child.NextVisible(true);

		while (!SerializedProperty.EqualContents(child, end))
		{
			height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;

			if (!child.NextVisible(false))
			{
				break;
			}
		}

		var buttons = GetButtons(property);

		if (buttons != null)
		{
			foreach (var method in buttons)
			{
				height += ButtonParameterDrawer.GetHeight(method) + EditorGUIUtility.standardVerticalSpacing;
			}
		}

		return Mathf.Max(height - EditorGUIUtility.standardVerticalSpacing, EditorGUIUtility.singleLineHeight);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (!IsInline(property))
		{
			EditorGUI.PropertyField(position, property, attribute is HideLabelAttribute ? GUIContent.none : label, true);

			return;
		}

		var child = property.Copy();
		var end = property.GetEndProperty();

		child.NextVisible(true);

		while (!SerializedProperty.EqualContents(child, end))
		{
			var childHeight = EditorGUI.GetPropertyHeight(child, true);

			EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, childHeight), child, true);
			position.y += childHeight + EditorGUIUtility.standardVerticalSpacing;

			if (!child.NextVisible(false))
			{
				break;
			}
		}

		var buttons = GetButtons(property);

		if (buttons == null)
		{
			return;
		}

		var target = PropertyUtility.GetTargetObjectOfProperty(property);

		foreach (var method in buttons)
		{
			var buttonAttribute = (ButtonAttribute)method.GetCustomAttributes(typeof(ButtonAttribute), true)[0];
			var text = string.IsNullOrEmpty(buttonAttribute.Text) ? ObjectNames.NicifyVariableName(method.Name) : buttonAttribute.Text;
			var buttonHeight = ButtonParameterDrawer.GetHeight(method);
			var buttonRect = new Rect(position.x, position.y, position.width, buttonHeight);
			var path = ButtonParameterDrawer.HasEditableParameters(method) ? property.propertyPath : string.Empty;

			if (ButtonParameterDrawer.Draw(buttonRect, method, property.serializedObject.targetObject, path, text, out var args))
			{
				property.serializedObject.ApplyModifiedProperties();
				method.Invoke(target, args);
				property.serializedObject.Update();
				EditorUtility.SetDirty(property.serializedObject.targetObject);
			}

			position.y += buttonHeight + EditorGUIUtility.standardVerticalSpacing;
		}
	}
}

[CustomPropertyDrawer(typeof(EnumToggleButtonsAttribute))]
public class EnumToggleButtonsAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Enum)
		{
			EditorGUI.PropertyField(position, property, label);

			return;
		}

		EditorGUI.BeginProperty(position, label, property);

		var rect = EditorGUI.PrefixLabel(position, label);
		var names = property.enumDisplayNames;
		var width = rect.width / Mathf.Max(1, names.Length);
		var isFlags = fieldInfo.FieldType.IsDefined(typeof(FlagsAttribute), false) ||
		              (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericArguments().Any(t => t.IsDefined(typeof(FlagsAttribute), false)));

		for (var i = 0; i < names.Length; i++)
		{
			var buttonRect = new Rect(rect.x + width * i, rect.y, width, EditorGUIUtility.singleLineHeight);
			var style = names.Length == 1 ? EditorStyles.miniButton : i == 0 ? EditorStyles.miniButtonLeft : i == names.Length - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
			var value = isFlags ? (property.intValue & (1 << i)) != 0 : property.enumValueIndex == i;

			EditorGUI.BeginChangeCheck();
			value = GUI.Toggle(buttonRect, value, names[i], style);

			if (EditorGUI.EndChangeCheck())
			{
				if (isFlags)
				{
					property.intValue = value ? property.intValue | (1 << i) : property.intValue & ~(1 << i);
				}
				else
				{
					property.enumValueIndex = i;
				}
			}
		}

		EditorGUI.EndProperty();
	}
}
