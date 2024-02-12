using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class DecompiledScriptModule {
        public static void PatchAll(LCPatcherSettings settings, bool outputCopy) {
            var assetRipperPath = ModuleUtility.AssetRipperTempDirectoryExportedProject;
            var scriptsFolder = Path.Combine(assetRipperPath, "Assets", "Scripts", "Assembly-CSharp");
            
            // delete all .copy.cs and .copy.copy.cs
            foreach (var file in Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories)) {
                if (file.EndsWith(".copy.cs") || file.EndsWith(".copy.copy.cs")) {
                    File.Delete(file);
                }
            }
            
            var scriptFiles = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories);
        
            EditorUtility.DisplayProgressBar("Cleaning decompiled scripts", "Cleaning decompiled scripts", 0.2f);
            LCProjectPatcherScriptCleaner.ScriptScrubber.ScrubDecompiledScript(scriptFiles, outputCopy, Debug.Log);
            EditorUtility.ClearProgressBar();
        
            if (!outputCopy) {
                var outputRootFolder = settings.GetLethalCompanyGamePath();
                string projectScriptsFolder;
                if (settings.AssetRipperSettings.TryGetMapping("Scripts", out var finalFolder)) {
                    projectScriptsFolder = Path.Combine(outputRootFolder, finalFolder);
                } else {
                    projectScriptsFolder = Path.Combine(outputRootFolder, "Scripts");
                }

                // if (AssetRipperModule.HasDunGenAsset) {
                //     var dunGenPath = Path.Combine(assetRipperPath, "Assets", "Scripts", "Assembly-CSharp", "DunGen");
                //     if (Directory.Exists(dunGenPath)) {
                //         Directory.Delete(dunGenPath, recursive: true);
                //     }
                // }

                var hasDunGen = AssetRipperModule.HasDunGenAsset;
                EditorUtility.DisplayProgressBar("Cloning scripts", "Cloning scripts", 0.8f);
                for (var i = 0; i < scriptFiles.Length; i++) {
                    var file = scriptFiles[i];
                    if (!File.Exists(file)) {
                        Debug.LogWarning($"File {file} does not exist");
                        continue;
                    }
                    
                    if (hasDunGen && file.Contains("DunGen")) {
                        Debug.LogWarning($"- Skipping {file}");
                        continue;
                    }
                    
                    EditorUtility.DisplayProgressBar("Cloning scripts", $"Cloning {file}", (float)i / scriptFiles.Length);
                    var relativePath = file.Replace(Path.Combine(assetRipperPath, "Assets", "Scripts"), projectScriptsFolder);
                    Debug.Log($"Creating {relativePath} at {Path.GetDirectoryName(relativePath)}");
                    var directory = Path.GetDirectoryName(relativePath);
                    if (directory == null) {
                        Debug.LogWarning($"Directory for {relativePath} does not exist");
                        continue;
                    }
                    
                    Directory.CreateDirectory(directory);
                    File.Copy(file, relativePath, overwrite: true);
                }
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
