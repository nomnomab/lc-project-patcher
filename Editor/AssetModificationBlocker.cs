using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor {
    public class AssetModificationBlocker: AssetModificationProcessor {
        private static bool _isExiting;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnLoad() {
            var runtimeSettings = ModuleUtility.GetPatcherRuntimeSettings();
            if (runtimeSettings.DisableAutomaticScriptableObjectReloading) {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                return;
            }
            
            _isExiting = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj) {
            var runtimeSettings = ModuleUtility.GetPatcherRuntimeSettings();
            if (runtimeSettings.DisableAutomaticScriptableObjectReloading) {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                return;
            }
            
            if (obj == PlayModeStateChange.EnteredEditMode) {
                _isExiting = true;
                AssetDatabase.SaveAssets();
            }
        }

        private static string[] OnWillSaveAssets(string[] paths) {
            var runtimeSettings = ModuleUtility.GetPatcherRuntimeSettings();
            if (runtimeSettings.DisableAutomaticScriptableObjectReloading) {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                return paths;
            }
            
            if (Application.isPlaying || _isExiting) {
                if (_isExiting) {
                    _isExiting = false;
                    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                }

                var list = new List<string>();
                var settings = ModuleUtility.GetPatcherSettings();
                var lcPath = settings.GetLethalCompanyGamePath();
                string soPath;
                if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
                    soPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
                } else {
                    soPath = Path.Combine(settings.GetLethalCompanyGamePath(), "MonoBehaviour");
                }
                
                foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new string[] { soPath })) {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains("UnityEngine")) continue;
                    list.Add(path);
                }

                Debug.LogWarning("You can't save assets while in play mode");
                foreach (var path in paths) {
                    Debug.LogWarning($"- blocked: \"{path}\"");
                }

                try {
                    var allPaths = paths.Concat(list).ToArray();
                    foreach (var path in allPaths) {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (asset.GetType().FullName.Contains("Unity")) continue;
                        Resources.UnloadAsset(asset);
                    }

                    // AssetDatabase.StartAssetEditing();
                    // AssetDatabase.ForceReserializeAssets(allPaths);
                    // foreach (var path in allPaths) {
                    //     AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    // }
                    // AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                } catch (Exception e) {
                    Debug.LogError(e);
                }

                return Array.Empty<string>();
            }
            
            return paths;
        }
    }
}
