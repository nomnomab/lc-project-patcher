using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class FinalizerModule {
        public static void PatchSceneList(LCPatcherSettings settings) {
            string scenesPath;
            if (settings.AssetRipperSettings.TryGetMapping("Scenes", out var finalFolder)) {
                scenesPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                scenesPath = Path.Combine(settings.GetLethalCompanyGamePath(), "Scenes");
            }
            var scenes = AssetDatabase.FindAssets("t:SceneAsset", new[] {scenesPath})
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            
            // move InitSceneLaunchOptions to the top
            var initScene = scenes.FirstOrDefault(scene => scene.Contains("InitSceneLaunchOptions"));
            if (initScene == null) {
                Debug.LogError("Could not find InitSceneLaunchOptions");
                return;
            }
            
            scenes = scenes.Where(scene => scene != initScene).Prepend(initScene).ToArray();
            
            // build scenes out
            EditorBuildSettings.scenes = scenes
                .Select(scene => new EditorBuildSettingsScene(scene, true))
                .ToArray();
        }

        public static void OpenInitScene() {
            var initScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.path.Contains("InitSceneLaunchOptions"));
            if (initScene == null) {
                Debug.LogError("Could not find InitSceneLaunchOptions");
                return;
            }
            EditorSceneManager.OpenScene(initScene.path);
        }

        public static void PatchES3DefaultsScriptableObject(LCPatcherSettings settings) {
            var gamePath = settings.GetLethalCompanyGamePath(fullPath: true);
            string resources;
            if (settings.AssetRipperSettings.TryGetMapping("Resources", out var finalFolder)) {
                resources = Path.Combine(gamePath, finalFolder);
            } else {
                resources = Path.Combine(gamePath, "Resources");
            }
            
            string scripts;
            if (settings.AssetRipperSettings.TryGetMapping("Scripts", out finalFolder)) {
                scripts = Path.Combine(gamePath, finalFolder);
            } else {
                scripts = Path.Combine(gamePath, "Scripts");
            }
            
            var es3DefaultsScriptsPath = Path.Combine(scripts, "es3", "ES3Defaults.cs.meta");
            var metaText = File.ReadAllText(es3DefaultsScriptsPath);
            var guid = GuidPatcherModule.GuidPattern.Match(metaText);
            if (!guid.Success) {
                Debug.LogError("Could not find guid in ES3Defaults.cs");
                return;
            }
                    
            var guidString = guid.Groups["guid"].Value;
            var es3DefaultsResourcesPath = Path.Combine(resources, "es3", "ES3Defaults.asset");
            var text = File.ReadAllText(es3DefaultsResourcesPath);
            text = GuidPatcherModule.GuidPattern.Replace(text, $"guid: {guidString}");
            File.WriteAllText(es3DefaultsResourcesPath, text);
        }

        public static void PatchHDRPVolumeProfile(LCPatcherSettings settings) {
            var settingsPath = settings.GetNativePath();
            
            string soPath;
            if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), "MonoBehaviour");
            }
            
            var hdrpSettingsPath = Path.Combine(settingsPath, "Settings", "HDRPDefaultResources", "HDRenderPipelineGlobalSettings.asset");
            var hdrpSettings = AssetDatabase.LoadAssetAtPath<Object>(hdrpSettingsPath);
            if (hdrpSettings) {
                var serializedObject = new SerializedObject(hdrpSettings);
                var volumeProfile = serializedObject.FindProperty("m_DefaultVolumeProfile");
                if (volumeProfile != null) {
                    var newSettingsPath = Path.Combine(soPath, "DefaultSettingsVolumeProfile.asset");
                    var newSettings = AssetDatabase.LoadAssetAtPath<Object>(newSettingsPath);
                    if (newSettings) {
                        volumeProfile.objectReferenceValue = newSettings;
                        serializedObject.ApplyModifiedProperties();
                    } else {
                        Debug.LogWarning($"Could not find DefaultSettingsVolumeProfile at \"{newSettingsPath}\"");
                    }
                }
            } else {
                Debug.LogError($"Could not find HDRenderPipelineGlobalSettings at \"{hdrpSettingsPath}\"");
            }
        }

        public static void PatchQualityPipelineAsset(LCPatcherSettings settings) {
            string sos;
            if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
                sos = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                sos = Path.Combine(settings.GetLethalCompanyGamePath(), "MonoBehaviour");
            }

            var pipelineAssetPath = Path.Combine(sos, "HDRenderPipelineAsset.asset");
            var hdRenderPipelineAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(pipelineAssetPath);
            if (!hdRenderPipelineAsset) {
                Debug.LogError($"Could not find HDRenderPipelineAsset at \"{pipelineAssetPath}\"");
                return;
            }
            QualitySettings.renderPipeline = hdRenderPipelineAsset;
        }

        public static void PatchDiageticAudioMixer(LCPatcherSettings settings) {
            var gamePath = settings.GetLethalCompanyGamePath(fullPath: true);
            string audioMixerControllerPath;
            if (settings.AssetRipperSettings.TryGetMapping("AudioMixerController", out var finalFolder)) {
                audioMixerControllerPath = Path.Combine(gamePath, finalFolder);
            } else {
                audioMixerControllerPath = Path.Combine(gamePath, "AudioMixerController");
            }

            var diageticPath = Path.Combine(audioMixerControllerPath, "Diagetic.mixer");
            try {
                var text = File.ReadAllText(diageticPath);
                var lines = text.Split('\n');
                var finalText = new StringBuilder();
                for (var i = 0; i < lines.Length; i++) {
                    finalText.AppendLine(lines[i].TrimEnd());
                
                    if (!lines[i].Contains("m_EffectName: Echo")) {
                        continue;
                    }
                
                    for (var j = i + 1; j < lines.Length; j++) {
                        if (lines[j].Contains("m_Bypass:")) {
                            finalText.AppendLine("  m_Bypass: 1");
                            i = j;
                            break;
                        }
                    
                        finalText.AppendLine(lines[j].TrimEnd());
                    }
                }
                File.WriteAllText(diageticPath, finalText.ToString());
            } catch (System.Exception e) {
                Debug.LogError(e);
            }
        }

        public static void SortScriptableObjectFolder(LCPatcherSettings settings) {
            string soPath;
            if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), "MonoBehaviour");
            }
            
            var allScriptableObjects = AssetDatabase.FindAssets("t:ScriptableObject", new[] {soPath})
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .Where(x => x)
                .ToArray();
            
            // sort by type
            using var _ = DictionaryPool<Type, List<ScriptableObject>>.Get(out var mappings);
            foreach (var so in allScriptableObjects) {
                var type = so.GetType();
                if (!mappings.TryGetValue(type, out var list)) {
                    list = new List<ScriptableObject>();
                    mappings.Add(type, list);
                }
                list.Add(so);
            }
            
            AssetDatabase.StartAssetEditing();
            using var __ = ListPool<string>.Get(out var foldersUsed);
            try {
                foreach (var group in mappings.Where(x => x.Value.Count > 1)) {
                    Debug.Log($"{group.Key} -> {group.Value.Count}");
                
                    var type = group.Key;
                    var rootName = string.IsNullOrEmpty(type.Namespace) ? string.Empty : type.Namespace.Split('.')[0];
                    var folderPath = string.IsNullOrEmpty(rootName) ? Path.Combine(soPath, type.Name) : Path.Combine(soPath, rootName, type.Name);
                    var allFolders = folderPath.Replace(soPath, string.Empty).Split(Path.DirectorySeparatorChar);
                    var totalPath = soPath;
                    foreach (var folder in allFolders) {
                        var currentPath = Path.Combine(totalPath, folder);

                        if (!foldersUsed.Contains(currentPath)) {
                            foldersUsed.Add(currentPath);
                            if (!AssetDatabase.IsValidFolder(folder)) {
                                Debug.Log($"Creating folder: {folder} in {totalPath}");
                                AssetDatabase.CreateFolder(totalPath, folder);
                                AssetDatabase.StopAssetEditing();
                                AssetDatabase.StartAssetEditing();
                            }
                        }
                        totalPath = currentPath;
                    }
                
                    foreach (var so in group.Value) {
                        var assetPath = AssetDatabase.GetAssetPath(so);
                        var newPath = Path.Combine(folderPath, Path.GetFileName(assetPath));
                        if (assetPath != newPath) {
                            AssetDatabase.MoveAsset(assetPath, newPath);
                        }
                    }
                }
            } catch {
                // ignored
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }
        }
        
        public static void UnSortScriptableObjectFolder(LCPatcherSettings settings) {
            string soPath;
            if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), "MonoBehaviour");
            }
            
            var allScriptableObjects = AssetDatabase.FindAssets("t:ScriptableObject", new[] {soPath})
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .Where(x => x)
                .ToArray();
            
            AssetDatabase.StartAssetEditing();
            try {
                // move all back into MonoBehaviour location and remove other folders
                foreach (var so in allScriptableObjects) {
                    var assetPath = AssetDatabase.GetAssetPath(so);
                    var newPath = Path.Combine(soPath, Path.GetFileName(assetPath));
                    if (assetPath != newPath) {
                        AssetDatabase.MoveAsset(assetPath, newPath);
                    }
                }

                var folders = AssetDatabase.GetSubFolders(soPath);
                foreach (var folder in folders) {
                    AssetDatabase.DeleteAsset(folder);
                }
            } catch {
                // ignored
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }
        }
        
        public static void SortPrefabsFolder(LCPatcherSettings settings) {
            string prefabsPath;
            if (settings.AssetRipperSettings.TryGetMapping("Prefabs", out var finalFolder)) {
                prefabsPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                prefabsPath = Path.Combine(settings.GetLethalCompanyGamePath(), "Prefabs");
            }
            
            var allPrefabs = AssetDatabase.FindAssets("t:GameObject", new[] {prefabsPath})
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(x => x)
                .Select(x => new {
                    prefab = x,
                    component = getFirstComponent(x)
                })
                .ToArray();
            
            // var prefabTypeCounts = allPrefabs
            //     .GroupBy(x => x.GetComponents<MonoBehaviour>().FirstOrDefault()?.GetType().Name)
            //     .Where(x => x.Key != null)
            //     .ToDictionary(x => x.Key, x => x.Count());
            
            // sort by type
            using var _ = DictionaryPool<Type, List<GameObject>>.Get(out var mappings);
            foreach (var data in allPrefabs) {
                var firstComponent = data.component;
                if (firstComponent) {
                    if (!mappings.TryGetValue(firstComponent.GetType(), out var list)) {
                        list = new List<GameObject>();
                        mappings.Add(firstComponent.GetType(), list);
                    }
                    list.Add(data.prefab);
                }
            }

            AssetDatabase.StartAssetEditing();
            using var __ = ListPool<string>.Get(out var foldersUsed);
            try {
                foreach (var (key, prefabs) in mappings) {
                    var type = key;
                    if (type != null && prefabs.Count == 1) {
                        // check base type instead
                        var baseType = type.BaseType;
                        if (baseType != null && allPrefabs.Count(x => x.component && baseType.IsAssignableFrom(x.component.GetType())) > 1) {
                            type = baseType;
                        }
                    }
                    
                    if (type == typeof(Component)) continue;
                    if (prefabs.Count == 0 || type == null) continue;

                    var folder = Path.Combine(prefabsPath, type.Name);
                    if (!foldersUsed.Contains(folder)) {
                        foldersUsed.Add(folder);
                        if (!AssetDatabase.IsValidFolder(folder)) {
                            AssetDatabase.CreateFolder(prefabsPath, type.Name);
                            AssetDatabase.StopAssetEditing();
                            AssetDatabase.StartAssetEditing();
                        }
                    }

                    foreach (var prefab in prefabs) {
                        Debug.Log($"{prefab} -> {type} [{prefabs.Count}]");

                        var assetPath = AssetDatabase.GetAssetPath(prefab);
                        var newPath = Path.Combine(folder, Path.GetFileName(assetPath));
                        Debug.Log($"AssetPath: {assetPath} -> {newPath}");
                        if (assetPath != newPath) {
                            AssetDatabase.MoveAsset(assetPath, newPath);
                        }
                    }
                }
            } catch {
                // ignored
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            static Component getFirstComponent(GameObject obj) {
                var components = obj.GetComponentsInChildren<Component>();
                Component firstNoneUnityComponent = null;
                Component firstUnityComponent = null;
                foreach (var component in components) {
                    if (!component) continue;
                    if (component is Transform) continue;

                    var @namespace = component.GetType().Namespace;
                    if (string.IsNullOrEmpty(@namespace)) {
                        @namespace = string.Empty;
                    }
                    
                    if (@namespace.StartsWith("Unity")) {
                        if (!firstUnityComponent) {
                            firstUnityComponent = component;
                        }
                        continue;
                    }
                    
                    // return component;
                    if (!firstNoneUnityComponent) {
                        firstNoneUnityComponent = component;
                        break;
                    }
                }
                
                if (firstNoneUnityComponent) {
                    return firstNoneUnityComponent;
                }
                
                return firstUnityComponent;
            }
        }
        
        public static void UnSortPrefabsFolder(LCPatcherSettings settings) {
            string prefabsPath;
            if (settings.AssetRipperSettings.TryGetMapping("Prefabs", out var finalFolder)) {
                prefabsPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                prefabsPath = Path.Combine(settings.GetLethalCompanyGamePath(), "Prefabs");
            }
            
            var allPrefabs = AssetDatabase.FindAssets("t:GameObject", new[] {prefabsPath})
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(x => x)
                .ToArray();
            
            AssetDatabase.StartAssetEditing();
            try {
                // move all back into Prefabs location and remove other folders
                foreach (var prefab in allPrefabs) {
                    var assetPath = AssetDatabase.GetAssetPath(prefab);
                    var newPath = Path.Combine(prefabsPath, Path.GetFileName(assetPath));
                    if (assetPath != newPath) {
                        AssetDatabase.MoveAsset(assetPath, newPath);
                    }
                }
            
                var folders = AssetDatabase.GetSubFolders(prefabsPath);
                foreach (var folder in folders) {
                    AssetDatabase.DeleteAsset(folder);
                }
            } catch {
                // ignored
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }
        }

        public static void ChangeGameViewResolution() {
            if (GameViewUtils.TrySetSize("16:9")) {
                Debug.Log("GameView resolution set to 16:9");
            } else {
                Debug.LogWarning("Could not set GameView resolution to 16:9");
            }
        }
    }
}
