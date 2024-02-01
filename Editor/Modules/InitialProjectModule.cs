using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class InitialProjectModule {
        public static void MoveNativeFiles(LCPatcherSettings settings) {
            ModuleUtility.CreateDirectory(settings.GetBaseUnityPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetBaseLethalCompanyPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetNativePath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetAssetStorePath(fullPath: true));
            
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            
            var unityDirectory = settings.GetBaseUnityPath();
            var lethalCompanyDirectory = settings.GetBaseLethalCompanyPath();
            
            var projectRoot = settings.GetBasePath();
            var nativeDirectory = settings.GetNativePath();

            var assetPaths = AssetDatabase
                .GetAllAssetPaths()
                .Where(x => Path.GetDirectoryName(x) == projectRoot && isValidPath(x))
                .ToArray();
            for (var i = 0; i < assetPaths.Length; i++) {
                var assetPath = assetPaths[i];
                EditorUtility.DisplayProgressBar("Moving files", $"Moving {assetPath}", (float)i / assetPaths.Length);
                
                var error = AssetDatabase.MoveAsset(assetPath, assetPath.Replace(projectRoot, nativeDirectory));
                if (!string.IsNullOrEmpty(error)) {
                    Debug.LogError($"Error moving asset {assetPath}: {error}");
                }
            }
            EditorUtility.ClearProgressBar();
            
            bool isValidPath(string path) {
                path = path.Replace('/', Path.DirectorySeparatorChar);
                return !path.StartsWith(unityDirectory) && !path.StartsWith(lethalCompanyDirectory);
            }
        }
    }
}
