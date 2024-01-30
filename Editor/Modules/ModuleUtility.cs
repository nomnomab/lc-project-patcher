using System.IO;
using UnityEditor;

namespace Nomnom.LCProjectPatcher.Modules {
    public static class ModuleUtility {
        public static void CopyFilesRecursively(string sourcePath, string targetPath) {
            Directory.CreateDirectory(targetPath);
            
            foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }
            
            foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)) {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), overwrite: true);
            }
        }
        
        public static string GetLethalCompanyDataFolder() {
            return EditorPrefs.GetString("nomnom.lc_project_patcher.lc_data_folder");
        }
        
        public static string GetAssetRipperDirectory() {
            return EditorPrefs.GetString("nomnom.lc_project_patcher.asset_ripper_path");
        }

        public static string GetAssetRipperCloneDirectory() {
            var assetRipperPath = GetAssetRipperDirectory();
            var directory = Path.GetDirectoryName(assetRipperPath);
            if (directory == null) {
                throw new DirectoryNotFoundException("Could not find AssetRipper directory");
            }
            
            return Path.Combine(directory, "ExportedProject_Modified");
        }
    }
}
