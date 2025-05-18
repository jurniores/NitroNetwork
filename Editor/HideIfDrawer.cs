#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(HideIfAttribute))]
public class HideIfDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        HideIfAttribute hideIf = (HideIfAttribute)attribute;
        SerializedProperty boolProp = property.serializedObject.FindProperty(hideIf.boolFieldName);

        bool shouldHide = boolProp != null && boolProp.boolValue == hideIf.hideIfTrue;
        if (!shouldHide)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        HideIfAttribute hideIf = (HideIfAttribute)attribute;
        SerializedProperty boolProp = property.serializedObject.FindProperty(hideIf.boolFieldName);

        bool shouldHide = boolProp != null && boolProp.boolValue == hideIf.hideIfTrue;
        return shouldHide ? 0 : EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif