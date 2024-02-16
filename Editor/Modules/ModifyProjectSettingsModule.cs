using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class ModifyProjectSettingsModule {
        private readonly static string[] FilesToCopy = new[] {
            "NavMeshAreas.asset",
            "TagManager.asset",
            "TimeManager.asset",
            "DynamicsManager.asset",
            "QualitySettings.asset"
        };
        
        public static void CopyOverProjectSettings() {
            var assetRipperPath = ModuleUtility.AssetRipperTempDirectoryExportedProject;
            PlayerSettings.allowUnsafeCode = true;
            
            var assetRipperSettingsFolder = Path.Combine(assetRipperPath, "ProjectSettings");
            var projectSettingsFolder = Path.Combine(Application.dataPath, "..", "ProjectSettings");
            
            var files = Directory.GetFiles(assetRipperSettingsFolder, "*.asset")
                .Where(x => FilesToCopy.Contains(Path.GetFileName(x)));

            foreach (var file in files) {
                var fileName = Path.GetFileName(file);
                var projectFile = Path.Combine(projectSettingsFolder, fileName);
                File.Copy(file, projectFile, true);
            }
            
            Debug.Log("Project settings copied");
        }
    }
}
