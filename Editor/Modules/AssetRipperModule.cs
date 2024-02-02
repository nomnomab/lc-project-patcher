using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class AssetRipperModule {
        public readonly static string[] InitialFolders = {
            "AudioMixerController"
        };
        
        public readonly static string[] FinalFolders = {
            "AnimationClip",
            "AnimatorController",
            "AudioClip",
            // "AudioMixerController",
            "Cubemap",
            "Font",
            "LightingSettings",
            "Material",
            "Mesh",
            "MonoBehaviour",
            "NavMeshData",
            "PhysicMaterial",
            "PrefabInstance",
            "RenderTexture",
            "Resources",
            "Scenes",
            "Sprite",
            "TerrainData",
            "TerrainLayer",
            "Texture2D",
            "Texture3D",
            "VideoClip"
        };

        public static UniTask RunAssetRipper(LCPatcherSettings settings) {
            var assetRipperExePath = ModuleUtility.AssetRipperDirectory;
            var pathToData = ModuleUtility.LethalCompanyDataFolder;
            var outputPath = ModuleUtility.AssetRipperTempDirectory;

            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
            
            Directory.CreateDirectory(outputPath);

            // run asset ripper
            Debug.Log($"Running AssetRipper at \"{assetRipperExePath}\" with \"{pathToData}\" and outputting into \"{outputPath}\"");
            Debug.Log($"Using data folder at \"{pathToData}\"");
            Debug.Log($"Outputting ripped assets at \"{outputPath}\"");

            var process = new System.Diagnostics.Process {
                StartInfo = new System.Diagnostics.ProcessStartInfo {
                    FileName = assetRipperExePath,
                    Arguments = $"\"{pathToData}\" \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            try {
                process.Start();

                var elapsed = 0f;
                while (!process.StandardOutput.EndOfStream) {
                    var line = process.StandardOutput.ReadLine();
                    //? time estimation
                    elapsed += Time.deltaTime / (60f * 3);
                    EditorUtility.DisplayProgressBar("Running AssetRipper", line, elapsed);
                }
                EditorUtility.ClearProgressBar();
                process.WaitForExit();

                // check for any errors
                if (process.ExitCode != 0) {
                    throw new Exception("AssetRipper failed to run");
                }
                
                RemoveDunGenFromOutputIfNeeded(settings, outputPath);
                
                // ? copy the files from the Temp folder into a temp folder in the project
                // ModuleUtility.CopyFilesRecursively(outputPath, ModuleUtility.AssetRipperTempDirectory);
            } catch (Exception e) {
                Debug.LogError(e);
                throw;
            }
            
            return UniTask.CompletedTask;
        }

        private static void RemoveDunGenFromOutputIfNeeded(LCPatcherSettings settings, string outputFolder) {
            // ? check if we have DunGen in the project already
            var nativePath = settings.GetAssetStorePath(fullPath: true);
            var assetDunGenPath = Path.Combine(nativePath, "DunGen");
            if (!Directory.Exists(assetDunGenPath)) {
                return;
            }
            
            // remove DunGen from the Asset Ripper output
            var assetRipperDunGenPath = Path.Combine(outputFolder, "Scripts", "Assembly-CSharp", "DunGen");
            Directory.Delete(assetRipperDunGenPath, recursive: true);
                
            // import the navmesh package from the asset
            EditorUtility.DisplayProgressBar("Installing packages", "Installing DunGen NavMesh package", 0.75f);
            var packagepath = Path.Combine(settings.GetAssetStorePath(), "DunGen", "Integration", "Unity NavMesh.unitypackage");
            AssetDatabase.ImportPackage(packagepath, false);
            EditorUtility.ClearProgressBar();
        }
        
        // public static void CopyAssetRipperScripts(LCPatcherSettings settings) {
        //     var assetRipperSettings = settings.AssetRipperSettings;
        //     var outputRootFolder = settings.GetLethalCompanyGamePath(fullPath: true);
        //     
        //     var assetRipperTempFolder = ModuleUtility.AssetRipperTempDirectoryExportedProject;
        //     var assetsFolder = Path.Combine(assetRipperTempFolder, "Assets");
        //     
        //     string scriptsFolder;
        //     if (assetRipperSettings.TryGetMapping("Scripts", out var finalFolder)) {
        //         scriptsFolder = Path.Combine(outputRootFolder, finalFolder);
        //     } else {
        //         scriptsFolder = Path.Combine(outputRootFolder, "Scripts");
        //     }
        //     
        //     ModuleUtility.CopyFilesRecursively(Path.Combine(assetsFolder, "Scripts", "Assembly-CSharp"), scriptsFolder);
        // }
        
        public static void CopyAssetRipperContents(LCPatcherSettings settings) {
            var assetRipperSettings = settings.AssetRipperSettings;
            var outputRootFolder = settings.GetLethalCompanyGamePath();
            
            var assetRipperTempFolder = ModuleUtility.AssetRipperTempDirectoryExportedProject;
            var assetsFolder = Path.Combine(assetRipperTempFolder, "Assets");
            
            var folders = Directory.GetDirectories(assetsFolder);
            foreach (var folder in folders) {
                var folderName = Path.GetFileName(folder);
                if (folderName == "Scripts") {
                    continue;
                }
                
                // if (folder == folderName) continue;
                if (!assetRipperSettings.TryGetMapping(folderName, out var finalFolder)) {
                    continue;
                }
                
                var finalPath = Path.Combine(outputRootFolder, finalFolder);
                // Debug.Log($"{folder} maps to {finalPath}");
                
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) {
                    var finalFile = file.Replace(folder, finalPath);
                    var finalDirectory = Path.GetDirectoryName(finalFile);
                    if (!Directory.Exists(finalDirectory)) {
                        Directory.CreateDirectory(finalDirectory);
                    }
                    // Debug.Log($"Copying {file} to {finalFile}");

                    try {
                        File.Copy(file, finalFile, overwrite: true);
                    } catch (Exception e) {
                        Debug.LogError($"Failed to copy {file} to {finalFile}: {e}");
                    }
                }
            }

            // {
            //     string scriptsFolder;
            //     if (assetRipperSettings.TryGetMapping("Scripts", out var finalFolder)) {
            //         scriptsFolder = Path.Combine(outputRootFolder, finalFolder);
            //     } else {
            //         scriptsFolder = Path.Combine(outputRootFolder, "Scripts");
            //     }
            //     
            //     // trim scripts folder to only Assembly-CSharp
            //     var assemblyCSharpFolder = Path.Combine(scriptsFolder, "Assembly-CSharp");
            //     var scriptFolders = Directory.GetDirectories(scriptsFolder);
            //     foreach (var folder in scriptFolders) {
            //         if (Path.GetFileName(folder) == "Assembly-CSharp") {
            //             continue;
            //         }
            //         Directory.Delete(folder, recursive: true);
            //     }
            // }
        }
        
        public static void DeleteScriptsFromProject(LCPatcherSettings settings) {
            string scriptsFolder;
            if (settings.AssetRipperSettings.TryGetMapping("Scripts", out var finalFolder)) {
                scriptsFolder = Path.Combine(settings.GetLethalCompanyGamePath(fullPath: true), finalFolder);
            } else {
                scriptsFolder = Path.Combine(settings.GetLethalCompanyGamePath(fullPath: true), "Scripts");
            }
            
            if (Directory.Exists(scriptsFolder)) {
                Debug.Log($"Deleting {scriptsFolder}");
                Directory.Delete(scriptsFolder, recursive: true);
            }
        }

        public static void DeleteScriptableObjectsFromProject(LCPatcherSettings settings) {
            string soPath;
            if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(fullPath: true), finalFolder);
            } else {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(fullPath: true), "MonoBehaviour");
            }

            if (Directory.Exists(soPath)) {
                Debug.Log($"Deleting {soPath}");
                Directory.Delete(soPath, recursive: true);
            }
        }

        // public static UniTask PatchFix() {
        //     var assetRipperPath = ModuleUtility.AssetRipperCloneDirectory;
        //     
        //     // copy shadergraph shaders
        //     var shaderGraphPath = Path.Combine(assetRipperPath, "Assets", "Shader");
        //     var projectShaderGraphPath = Path.Combine(Application.dataPath, "Shader");
        //     Directory.CreateDirectory(projectShaderGraphPath);
        //
        //     var shaderGraphFiles = Directory
        //         .GetFiles(shaderGraphPath, "*.shader", SearchOption.AllDirectories)
        //         .Where(x => Path.GetFileNameWithoutExtension(x).StartsWith("Shader Graphs"))
        //         .ToArray();
        //
        //     for (var i = 0; i < shaderGraphFiles.Length; i++) {
        //         var file = shaderGraphFiles[i];
        //         var fileName = Path.GetFileName(file);
        //         var projectFilePath = Path.Combine(projectShaderGraphPath, fileName);
        //         EditorUtility.DisplayProgressBar("Copying Shader Graphs", $"Copying {fileName} to {projectFilePath}", (float)i / shaderGraphFiles.Length);
        //         File.Copy(file, projectFilePath, overwrite: true);
        //     }
        //
        //     for (var i = 0; i < FinalFolders.Length; i++) {
        //         var folder = FinalFolders[i];
        //         var ripperPath = Path.Combine(assetRipperPath, "Assets", folder);
        //         var projectPath = Path.Combine(Application.dataPath, folder);
        //         EditorUtility.DisplayProgressBar("Copying AssetRipper files", $"Copying {folder} to {projectPath}", (float)i / FinalFolders.Length);
        //         
        //         ModuleUtility.CopyFilesRecursively(ripperPath, projectPath);
        //         Debug.Log($"Copying {ripperPath} to {projectPath}");
        //     }
        //     
        //     // fix es3 defaults manually in Assets/Resources/es3/ES3Defaults.asset
        //     var filePath = Path.Combine(Application.dataPath, "Resources", "es3", "ES3Defaults.asset");
        //     var fileContents = File.ReadAllText(filePath);
        //     var match = GuidPatcherModule.GuidPattern.Match(fileContents);
        //     if (!match.Success) {
        //         throw new Exception("Could not find guid for ES3Defaults");
        //     }
        //     
        //     var es3Script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Resources/es3/ES3Defaults.cs");
        //     var globalId = GlobalObjectId.GetGlobalObjectIdSlow(es3Script);
        //     var assetGuid = globalId.assetGUID.ToString();
        //     var objectId = globalId.targetObjectId;
        //     Debug.Log($"Found ES3Defaults | {assetGuid} | {objectId}");
        //     fileContents = GuidPatcherModule.GuidPattern.Replace(fileContents, $"guid: {assetGuid}");
        //     Debug.Log($"Fixed ES3Defaults at {filePath} to {fileContents}");
        //             
        //     File.WriteAllText(filePath, fileContents);
        //     
        //     EditorUtility.ClearProgressBar();
        //
        //     return UniTask.CompletedTask;
        // }
    }
}
