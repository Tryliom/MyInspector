using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TitleAttribute))]
public class TitleAttributeDrawer : DecoratorDrawer
{
	private const float LineHeight = 1f;
	private const float Padding = 4f;

	public override float GetHeight()
	{
		return EditorGUIUtility.singleLineHeight + Padding * 2f + LineHeight;
	}

	public override void OnGUI(Rect position)
	{
		var title = ((TitleAttribute) attribute).Title;
		var labelRect = new Rect(position.x, position.y + Padding, position.width, EditorGUIUtility.singleLineHeight);

		EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

		var lineRect = EditorGUI.IndentedRect(new Rect(position.x, labelRect.yMax + 1f, position.width, LineHeight));

		EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.6f));
	}
}
