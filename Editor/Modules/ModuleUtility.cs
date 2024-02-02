using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class ModuleUtility {
        public static string LethalCompanyDataFolder => EditorPrefs.GetString("nomnom.lc_project_patcher.lc_data_folder");
        public static string AssetRipperDirectory => Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/AssetRipper~/AssetRipper.Tools.SystemTester.exe");
        public static string AssetRipperTempDirectory => GetProjectDirectory("AssetRipperOutput~");
        public static string AssetRipperTempDirectoryExportedProject => Path.Combine(AssetRipperTempDirectory, "ExportedProject");
        
        public static string ProjectDirectory => Application.dataPath;
        // public static string ProjectScriptsDirectory => Path.Combine(ProjectDirectory, "Scripts");
        // public static string ProjectResourcesDirectory => Path.Combine(ProjectDirectory, "Resources");
        
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
    }
}
