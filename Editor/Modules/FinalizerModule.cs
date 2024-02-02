using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
                    var newSettings = AssetDatabase.LoadAssetAtPath<Object>(Path.Combine(soPath, "DefaultSettingsVolumeProfile.asset"));
                    if (newSettings) {
                        volumeProfile.objectReferenceValue = newSettings;
                        serializedObject.ApplyModifiedProperties();
                    } else {
                        Debug.LogWarning("Could not find DefaultSettingsVolumeProfile");
                    }
                }
            } else {
                Debug.LogError("Could not find HDRenderPipelineGlobalSettings");
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
            var sorted = allScriptableObjects
                .GroupBy(x => x.GetType().Name)
                .Where(x => x.Key != null);

            foreach (var group in sorted) {
                var count = group.Count();
                if (count == 0) continue;
                if (count == 1) {
                    {
                        var first = group.First();
                        var assetPath = AssetDatabase.GetAssetPath(first);
                        var newPath = Path.Combine(soPath, Path.GetFileName(assetPath));
                        if (assetPath != newPath) {
                            AssetDatabase.MoveAsset(assetPath, newPath);
                        }
                    }
                    continue;
                }
                
                var type = group.Key;
                var folderPath = Path.Combine(soPath, type);
                if (!AssetDatabase.IsValidFolder(folderPath)) {
                    AssetDatabase.CreateFolder(soPath, type);
                }
                
                foreach (var so in group) {
                    try {
                        var assetPath = AssetDatabase.GetAssetPath(so);
                        var newPath = Path.Combine(folderPath, Path.GetFileName(assetPath));
                        if (assetPath != newPath) {
                            AssetDatabase.MoveAsset(assetPath, newPath);
                        }
                    } catch (System.Exception e) {
                        Debug.LogError(e);
                    }
                }
            }
        }
    }
}
