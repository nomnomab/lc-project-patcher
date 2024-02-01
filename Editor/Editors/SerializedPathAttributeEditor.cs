using System.IO;
using System.Reflection;
using Nomnom.LCProjectPatcher.Attributes;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Editors {
    [CustomPropertyDrawer(typeof(SerializedPathAttribute))]
    public class SerializedPathAttributeEditor: PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            // var isArray = property.propertyPath.Contains("Array");
            if (property.propertyType != SerializedPropertyType.String) {
                EditorGUI.LabelField(position, label.text, "Use SerializedPath with strings.");
                return;
            }

            var attribute = (SerializedPathAttribute)this.attribute;
            var nameOfGetterFunction = attribute.NameOfGetterFunction;
            // func() => string
            var targetObject = property.serializedObject.targetObject;
            var getterFunction = targetObject.GetType().GetMethod(nameOfGetterFunction, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (getterFunction == null) {
                EditorGUI.LabelField(position, label.text, $"No such function: {nameOfGetterFunction} in {targetObject.GetType().Name}");
                return;
            }
            
            var value = (string)getterFunction.Invoke(targetObject, null); 
            // value will be a prefix to the input field
            EditorGUI.BeginProperty(position, label, property);
            using (var changes = new EditorGUI.ChangeCheckScope()) {
                position.height -= 8 + 1 + 4;
                var rect = EditorGUI.PrefixLabel(position, label);
                var newValue = EditorGUI.TextField(rect, property.stringValue);
                var underlayRect = new Rect(rect.x, rect.y + position.height + 2, rect.width, 8);
                EditorGUI.LabelField(underlayRect, Path.Combine(value, property.stringValue), EditorStyles.miniLabel);

                if (changes.changed) {
                    property.stringValue = newValue;
                }
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return base.GetPropertyHeight(property, label) + 8 + 1 + 4;
        }
    }
}
