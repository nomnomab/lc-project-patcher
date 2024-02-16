using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class ModuleUtility {
        public static string LethalCompanyDataFolder {
            get {
                var path = EditorPrefs.GetString("nomnom.lc_project_patcher.lc_data_folder");
                if (string.IsNullOrEmpty(path)) {
                    return null;
                }
                
                if (!path.EndsWith("_Data")) {
                    var folderName = Path.GetFileNameWithoutExtension(path);
                    var dataFolder = $"{folderName}_Data";
                    path = Path.Combine(path, dataFolder);

                    if (!Directory.Exists(path)) {
                        Debug.LogError("The data path needs to end in \"_Data\"!");
                        return null;
                    }
                }

                return path;
            }
        }
        public static string AssetRipperDirectory => Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/AssetRipper~/AssetRipper.Tools.SystemTester.exe");
        public static string AssetRipperTempDirectory => GetProjectDirectory("..", "AssetRipperOutput~");
        public static string AssetRipperTempDirectoryExportedProject => Path.Combine(AssetRipperTempDirectory, "ExportedProject");
        public static string AssetRipperDllUrl => "https://github.com/nomnomab/AssetRipper/releases/download/v1.0.0/AssetRipper.SourceGenerated.dll.zip";
        
        public static string ProjectDirectory => Application.dataPath;
        
        public static string ActualExePath => Path.Combine(LethalCompanyDataFolder, "..", "Lethal Company.exe");
        public static string FakeExePath => Path.Combine(Application.dataPath, "..", "Lethal Company", "Lethal Company.exenot");
        public static string DirectoryPath => Path.GetDirectoryName(ActualExePath);
        public static string GameDataPath => Path.Combine(DirectoryPath, "Lethal Company_Data");
        public static string ManagedPath => Path.Combine(GameDataPath, "Managed");
        
        public static string GameExePath => GetPatcherRuntimeSettings().BepInExLocation switch {
            BepInExLocation.Local => FakeExePath,
            BepInExLocation.Game => ActualExePath,
            BepInExLocation.Custom => ActualExePath,
            _ => throw new ArgumentOutOfRangeException("BepInExLocation was given an invalid value")
        };
        
        public static string BepInExFolder => GetPatcherRuntimeSettings().BepInExLocation switch {
            BepInExLocation.Local => Path.Combine(Path.GetDirectoryName(FakeExePath)!, "BepInEx"),
            BepInExLocation.Custom => GetPatcherRuntimeSettings().CustomBepInExLocation,
            _ => Path.Combine(GameDataPath, "..", "BepInEx")
        };
        
        public static string GamePluginsPath => GetPatcherRuntimeSettings().BepInExLocation switch {
            BepInExLocation.Custom => Path.Combine(GetPatcherRuntimeSettings().CustomBepInExLocation.Replace('/', Path.DirectorySeparatorChar), "plugins"),
            _ => Path.Combine(Path.GetDirectoryName(GameExePath)!, "BepInEx", "plugins")
        };
        
        public static bool HasDomainReloadingDisabled {
            get {
                if (EditorSettings.enterPlayModeOptionsEnabled &&
                    EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload)) {
                    Debug.LogWarning("Domain reloading is disabled!");
                    return true;
                }
            
                return false;
            }
        }

        private static LCPatcherRuntimeSettings RuntimeInstance;
        
        public static void ResetInstance() {
            RuntimeInstance = null;
        }
        
        public static void CopyFilesRecursively(string sourceFolder, string targetFolder) {
            Directory.CreateDirectory(targetFolder);

            var paths = Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories);
            for (var i = 0; i < paths.Length; i++) {
                var dirPath = paths[i];
                EditorUtility.DisplayProgressBar("Copying files", $"Creating directory {dirPath}", (float)i / paths.Length);
                Directory.CreateDirectory(dirPath.Replace(sourceFolder, targetFolder));
            }

            var files = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++) {
                var newPath = files[i];
                EditorUtility.DisplayProgressBar("Copying files", $"Copying {newPath}", (float)i / files.Length);
                File.Copy(newPath, newPath.Replace(sourceFolder, targetFolder), overwrite: true);
            }
            
            EditorUtility.ClearProgressBar();
        }

        public static string GetProjectDirectory(params string[] items) {
            if (items.Length == 0) {
                return ProjectDirectory;
            }

            return Path.Combine(ProjectDirectory, Path.Combine(items));
        }

        public static DirectoryInfo CreateDirectory(string path) {
            if (Path.HasExtension(path)) {
                path = Path.GetDirectoryName(path);
            }
            
            return Directory.CreateDirectory(path);
        }

        public static LCPatcherSettings GetPatcherSettings() {
            var settings = AssetDatabase.FindAssets("t:LCPatcherSettings");
            if (settings.Length == 0) {
                throw new Exception("No LCPatcherSettings found in the project");
            }
            
            // ? if one exists outside of this package, use that instead
            var assetPath = AssetDatabase.GUIDToAssetPath(settings[0]);
            foreach (var setting in settings) {
                var tmpAssetPath = AssetDatabase.GUIDToAssetPath(setting);
                if (!tmpAssetPath.StartsWith("Packages/com.nomnom.lc-project-patcher")) {
                    assetPath = tmpAssetPath;
                    break;
                }
            }

            var asset = AssetDatabase.LoadAssetAtPath<LCPatcherSettings>(assetPath);
            if (!asset) {
                throw new Exception($"Failed to load LCPatcherSettings from \"{assetPath}\"");
            }
            
            return asset;
        }
        
        public static LCPatcherRuntimeSettings GetPatcherRuntimeSettings() {
            if (!Application.isPlaying) {
                RuntimeInstance = null;
            }
            
            if (RuntimeInstance) {
                return RuntimeInstance;
            }
            
            var settings = AssetDatabase.FindAssets("t:LCPatcherRuntimeSettings");
           
            // if one doesn't exist make one
            if (settings.Length == 0) {
                var asset = ScriptableObject.CreateInstance<LCPatcherRuntimeSettings>();
                AssetDatabase.CreateAsset(asset, "Assets/LCPatcherRuntimeSettings.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return asset;
            }

            RuntimeInstance = settings[0] switch {
                { } setting => AssetDatabase.LoadAssetAtPath<LCPatcherRuntimeSettings>(AssetDatabase.GUIDToAssetPath(setting)),
                _ => null
            };

            return RuntimeInstance;
        }
    }
}
