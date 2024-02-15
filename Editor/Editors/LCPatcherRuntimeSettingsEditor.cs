using System;
using System.Linq;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Editors {
    [CustomEditor(typeof(LCPatcherRuntimeSettings))]
    public class LCPatcherRuntimeSettingsEditor : UnityEditor.Editor {
        private static Type SelectableLevelType { get; } = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(x => x.GetName().Name == "Assembly-CSharp")
            ?.GetTypes()
            .FirstOrDefault(x => x.Name == "SelectableLevel");

        public override void OnInspectorGUI() {
            var obj = serializedObject;

            using (new EditorGUI.DisabledGroupScope(true)) {
                EditorGUILayout.PropertyField(obj.FindProperty("m_Script"));
            }

            using var changeCheck = new EditorGUI.ChangeCheckScope();

            // general
            var skipMainMenu = obj.FindProperty("SkipMainMenu");
            var useGameDirectoryForBepInEx = obj.FindProperty("BepInExLocation");
            var customBepInExDirectory = obj.FindProperty("CustomBepInExLocation");
            var loadProjectPlugins = obj.FindProperty("LoadProjectPlugins");

            EditorGUILayout.PropertyField(skipMainMenu);
            
            var bepInExLocation = (BepInExLocation)useGameDirectoryForBepInEx.enumValueIndex;

            switch (bepInExLocation) {
                case BepInExLocation.Custom:
                    var customDirectoryIsEmpty = string.IsNullOrEmpty(customBepInExDirectory.stringValue);
                    
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.PropertyField(useGameDirectoryForBepInEx);
                        GUI.enabled = !customDirectoryIsEmpty;
                        if (GUILayout.Button("Open", GUILayout.Width(50))) {
                            EditorApplication.delayCall += () => {
                                EditorUtility.RevealInFinder(ModuleUtility.BepInExFolder);
                            };
                        }
                        GUI.enabled = true;
                    }
                    
                    EditorGUILayout.PropertyField(customBepInExDirectory);

                    if (customDirectoryIsEmpty) {
                        EditorGUILayout.HelpBox("Custom BepInEx directory is empty!", MessageType.Warning);
                    }
                    break;
                default:
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.PropertyField(useGameDirectoryForBepInEx);
                        if (GUILayout.Button("Open", GUILayout.Width(50))) {
                            EditorApplication.delayCall += () => {
                                EditorUtility.RevealInFinder(ModuleUtility.BepInExFolder);
                            };
                        }
                    }
                    break;
            }
            
            EditorGUILayout.PropertyField(loadProjectPlugins);
            if (!loadProjectPlugins.boolValue) {
                EditorGUILayout.HelpBox("If the plugin exists in both the BepInEx directory and this project, the plugin in the project will always take priority in loading.", MessageType.Info);
            }

            // cheats
            // var autoLoadMoon = obj.FindProperty("AutoLoadMoon");

            // EditorGUILayout.Space();
            // EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            // EditorGUILayout.ObjectField(autoLoadMoon, SelectableLevelType, new GUIContent(autoLoadMoon.displayName));

            // experimental
            var loadPosterizationShader = obj.FindProperty("LoadPosterizationShader");
            
            EditorGUILayout.PropertyField(loadPosterizationShader);

            if (changeCheck.changed) {
                obj.ApplyModifiedProperties();
            }
        }
    }
}
