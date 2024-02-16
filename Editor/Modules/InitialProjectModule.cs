using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class InitialProjectModule {
        private readonly static string[] DefaultAssetPaths = {
            "Assets/OutdoorsScene.unity",
            "Assets/Readme.asset",
            "Assets/Settings",
            "Assets/TutorialInfo"
        };
        
        public static void MoveNativeFiles(LCPatcherSettings settings) {
            ModuleUtility.CreateDirectory(settings.GetBaseUnityPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetBaseLethalCompanyPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetNativePath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetAssetStorePath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetModsPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetToolsPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetResourcesPath(fullPath: true));

            var gamePath = settings.GetLethalCompanyGamePath(fullPath: true);
            if (Directory.Exists(gamePath)) {
                try {
                    Directory.Delete(gamePath, true);
                    Directory.CreateDirectory(gamePath);
                } catch (IOException e) {
                    Debug.LogError($"Error deleting game directory, are some plugins loaded? {e.Message}");
                }
            }
            
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            
            // var unityDirectory = settings.GetBaseUnityPath();
            // var lethalCompanyDirectory = settings.GetBaseLethalCompanyPath();
            
            var projectRoot = settings.GetBasePath();
            var nativeDirectory = settings.GetNativePath();

            // var assetPaths = AssetDatabase
            //     .GetAllAssetPaths()
            //     .Where(x => Path.GetDirectoryName(x) == projectRoot && isValidPath(x))
            //     .ToArray();
            for (var i = 0; i < DefaultAssetPaths.Length; i++) {
                var assetPath = DefaultAssetPaths[i];
                EditorUtility.DisplayProgressBar("Moving files", $"Moving {assetPath}", (float)i / DefaultAssetPaths.Length);
                
                if (!AssetDatabase.LoadAssetAtPath<Object>(assetPath)) continue;
                var error = AssetDatabase.MoveAsset(assetPath, assetPath.Replace(projectRoot, nativeDirectory));
                if (!string.IsNullOrEmpty(error)) {
                    Debug.LogWarning($"Error moving asset {assetPath}: {error}");
                }
            }
            EditorUtility.ClearProgressBar();
            
            // bool isValidPath(string path) {
            //     path = path.Replace('/', Path.DirectorySeparatorChar);
            //     return !path.StartsWith(unityDirectory) && !path.StartsWith(lethalCompanyDirectory);
            // }
        }
    }
}
