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
            var skipIntro = obj.FindProperty(nameof(LCPatcherRuntimeSettings.SkipIntro));
            var skipMainMenu = obj.FindProperty(nameof(LCPatcherRuntimeSettings.SkipMainMenu));
            var saveFileIndex = obj.FindProperty(nameof(LCPatcherRuntimeSettings.SaveFileIndex));
            var saveFileResetBeforeLoad = obj.FindProperty(nameof(LCPatcherRuntimeSettings.SaveFileResetBeforeLoad));
            
            EditorGUILayout.PropertyField(skipIntro);
            EditorGUILayout.PropertyField(skipMainMenu);
            EditorGUILayout.PropertyField(saveFileIndex);
            EditorGUILayout.PropertyField(saveFileResetBeforeLoad);
            
            var useGameDirectoryForBepInEx = obj.FindProperty(nameof(LCPatcherRuntimeSettings.BepInExLocation));
            var customBepInExDirectory = obj.FindProperty(nameof(LCPatcherRuntimeSettings.CustomBepInExLocation));
            var loadProjectPlugins = obj.FindProperty(nameof(LCPatcherRuntimeSettings.LoadProjectPlugins));
            
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
            var infiniteHealth = obj.FindProperty(nameof(LCPatcherRuntimeSettings.InfiniteHealth));
            var infiniteStamina = obj.FindProperty(nameof(LCPatcherRuntimeSettings.InfiniteStamina));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(infiniteHealth);
            EditorGUILayout.PropertyField(infiniteStamina);
            
            var autoLoadMoon = obj.FindProperty(nameof(LCPatcherRuntimeSettings.AutoLoadMoon));
            var autoLoadMoonSceneName = obj.FindProperty(nameof(LCPatcherRuntimeSettings.AutoLoadMoonSceneName));
            
            // EditorGUILayout.Space();
            // EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            GUI.enabled = string.IsNullOrEmpty(autoLoadMoonSceneName.stringValue);
            EditorGUILayout.ObjectField(autoLoadMoon, SelectableLevelType, new GUIContent(autoLoadMoon.displayName));
            GUI.enabled = true;
            
            GUI.enabled = !autoLoadMoon.objectReferenceValue;
            EditorGUILayout.PropertyField(autoLoadMoonSceneName);
            GUI.enabled = true;

            // experimental
            var loadPosterizationShader = obj.FindProperty(nameof(LCPatcherRuntimeSettings.LoadPosterizationShader));
            
            EditorGUILayout.PropertyField(loadPosterizationShader);

            if (changeCheck.changed) {
                obj.ApplyModifiedProperties();
            }
        }
    }
}
