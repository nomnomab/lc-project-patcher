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
            var settings = (LCPatcherRuntimeSettings)target;

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
            
            var bepInExLocation = obj.FindProperty(nameof(LCPatcherRuntimeSettings.BepInExLocation));
            var customBepInExDirectory = obj.FindProperty(nameof(LCPatcherRuntimeSettings.CustomBepInExLocation));
            var loadProjectPlugins = obj.FindProperty(nameof(LCPatcherRuntimeSettings.LoadProjectPlugins));
            
            var bepInExLocationValue = (BepInExLocation)bepInExLocation.enumValueIndex;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("BepInEx", EditorStyles.boldLabel);

            switch (bepInExLocationValue) {
                case BepInExLocation.Custom:
                    var customDirectoryIsEmpty = string.IsNullOrEmpty(customBepInExDirectory.stringValue);
                    
                    using (new EditorGUILayout.HorizontalScope()) {
                        // EditorGUILayout.PropertyField(bepInExLocation);
                        bepInExLocation.enumValueIndex = EditorGUILayout.Popup("BepInEx Location", bepInExLocation.enumValueIndex, bepInExLocation.enumDisplayNames);
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
                        // EditorGUILayout.PropertyField(bepInExLocation);
                        bepInExLocation.enumValueIndex = EditorGUILayout.Popup("BepInEx Location", bepInExLocation.enumValueIndex, bepInExLocation.enumDisplayNames);
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
            var startingCredits = obj.FindProperty(nameof(LCPatcherRuntimeSettings.StartingCredits));
            var time = obj.FindProperty(nameof(LCPatcherRuntimeSettings.Time));

            // EditorGUILayout.Space();
            // EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(infiniteHealth);
            EditorGUILayout.PropertyField(infiniteStamina);
            EditorGUILayout.PropertyField(startingCredits);
            
            time.floatValue = EditorGUILayout.Slider(time.displayName, time.floatValue, 0f, 1f);

            if (Application.isPlaying) {
                var timeOfDay = settings.GetTimeOfDay();
                var currentTime = settings.GetCurrentDayTimeField();
                var totalTime = settings.GetTotalTimeField();
                var globalTime = settings.GetGlobalTimeField();

                if (timeOfDay && currentTime != null && totalTime != null) {
                    EditorGUI.indentLevel++;
                    using (new EditorGUI.DisabledScope(true)) {
                        EditorGUILayout.FloatField("Current Time", (float)currentTime.GetValue(timeOfDay));
                        EditorGUILayout.FloatField("Total Time", (float)totalTime.GetValue(timeOfDay));
                        EditorGUILayout.FloatField("Global Time", (float)globalTime.GetValue(timeOfDay));
                        this.Repaint();
                    }
                    EditorGUI.indentLevel--;
                }
            }

            var autoLoadMoon = obj.FindProperty(nameof(LCPatcherRuntimeSettings.AutoLoadMoon));
            var autoLoadMoonReference = obj.FindProperty(nameof(LCPatcherRuntimeSettings.AutoLoadMoonReference));
            var autoLoadMoonSceneName = obj.FindProperty(nameof(LCPatcherRuntimeSettings.AutoLoadMoonSceneName));
            
            // EditorGUILayout.Space();
            // EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(autoLoadMoon);
            
            GUI.enabled = string.IsNullOrEmpty(autoLoadMoonSceneName.stringValue);
            EditorGUILayout.ObjectField(autoLoadMoonReference, SelectableLevelType, new GUIContent(autoLoadMoonReference.displayName));
            GUI.enabled = true;
            
            GUI.enabled = !autoLoadMoonReference.objectReferenceValue;
            EditorGUILayout.PropertyField(autoLoadMoonSceneName);
            GUI.enabled = true;

            // experimental
            var loadPosterizationShader = obj.FindProperty(nameof(LCPatcherRuntimeSettings.LoadPosterizationShader));
            
            EditorGUILayout.PropertyField(loadPosterizationShader);

            // other
            var disableAutomaticScriptableObjectReloading = obj.FindProperty(nameof(LCPatcherRuntimeSettings.DisableAutomaticScriptableObjectReloading));
            var disablePreInitScriptCoroutineReplacer = obj.FindProperty(nameof(LCPatcherRuntimeSettings.DisablePreInitScriptCoroutineReplacer));
            
            EditorGUILayout.PropertyField(disableAutomaticScriptableObjectReloading);
            
            if (disableAutomaticScriptableObjectReloading.boolValue) {
                EditorGUILayout.HelpBox("This will disable automatic reloading of ScriptableObjects when play mode stops. This will not revert any changes done to them, so expect missing/changed data.", MessageType.Warning);
            }
            
            EditorGUILayout.PropertyField(disablePreInitScriptCoroutineReplacer);
            
            if (disablePreInitScriptCoroutineReplacer.boolValue) {
                EditorGUILayout.HelpBox("This will disable the coroutine replacer for PreInitSceneScript coroutines. For mods like LLL that load things with that object, if SkipIntro is enabled, things may not load in correctly, or at all.", MessageType.Warning);
            }
            
            if (changeCheck.changed) {
                obj.ApplyModifiedProperties();
            }
        }
    }
}
