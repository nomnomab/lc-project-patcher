using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Modules {
    public static class DecompiledScriptModule {
        public static UniTask Patch(bool outputCopy) {
            var assetRipperPath = ModuleUtility.GetAssetRipperCloneDirectory();
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
                EditorUtility.DisplayProgressBar("Cloning scripts", "Cloning scripts", 0.8f);
                for (var i = 0; i < scriptFiles.Length; i++) {
                    var file = scriptFiles[i];
                    if (!File.Exists(file)) {
                        Debug.LogWarning($"File {file} does not exist");
                        continue;
                    }

                    Debug.Log($"Cloning {file}");
                    EditorUtility.DisplayProgressBar("Cloning scripts", $"Cloning {file}", (float)i / scriptFiles.Length);
                    var relativePath = file.Replace(Path.Combine(assetRipperPath, "Assets"), Path.Combine(Application.dataPath, "Scripts"));
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

            return UniTask.CompletedTask;
        }
    }
}
