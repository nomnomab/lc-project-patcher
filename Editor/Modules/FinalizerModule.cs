using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Modules {
    public static class FinalizerModule {
        public static UniTask PatchSceneList() {
            var scenes = AssetDatabase.FindAssets("t:SceneAsset", new[] {"Assets/Scenes"})
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            
            // move InitSceneLaunchOptions to the top
            var initScene = scenes.First(scene => scene.Contains("InitSceneLaunchOptions"));
            scenes = scenes.Where(scene => scene != initScene).Prepend(initScene).ToArray();
            
            // build scenes out
            EditorBuildSettings.scenes = scenes
                .Select(scene => new EditorBuildSettingsScene(scene, true))
                .ToArray();
            
            return UniTask.CompletedTask;
        }

        public static void OpenInitScene() {
            var initScene = EditorBuildSettings.scenes.First(scene => scene.path.Contains("InitSceneLaunchOptions"));
            EditorSceneManager.OpenScene(initScene.path);
        }

        public static UniTask PatchHDRPVolumeProfile() {
            var settings = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset");
            if (settings) {
                var serializedObject = new SerializedObject(settings);
                var volumeProfile = serializedObject.FindProperty("m_DefaultVolumeProfile");
                if (volumeProfile != null) {
                    var newSettings = AssetDatabase.LoadAssetAtPath<Object>("Assets/MonoBehaviour/DefaultSettingsVolumeProfile.asset");
                    volumeProfile.objectReferenceValue = newSettings;
                    serializedObject.ApplyModifiedProperties();
                }
            } else {
                Debug.LogError("Could not find HDRenderPipelineGlobalSettings");
            }
            
            return UniTask.CompletedTask;
        }
    }
}
