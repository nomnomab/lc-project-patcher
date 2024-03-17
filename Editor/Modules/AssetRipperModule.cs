using System;
using System.IO;
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

        public static async UniTask RunAssetRipper(LCPatcherSettings settings) {
            var assetRipperExePath = ModuleUtility.AssetRipperExecutable;
            var pathToData = ModuleUtility.LethalCompanyDataFolder;
            var outputPath = ModuleUtility.AssetRipperTempDirectory;

            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
            
            Directory.CreateDirectory(outputPath);
            
            // make sure we have the dll in-place
            var dllLocation = Path.Combine(Path.GetDirectoryName(assetRipperExePath), "AssetRipper.SourceGenerated.dll");
            if (!File.Exists(dllLocation)) {
                var dllUrl = ModuleUtility.AssetRipperDllUrl;
                var zipLocation = $"{dllLocation}.zip";
                EditorUtility.DisplayProgressBar("Downloading AssetRipper DLL", $"Downloading from {dllUrl}", 0.5f);
                
                using (var client = new System.Net.WebClient()) {
                    client.DownloadProgressChanged += (_, args) => {
                        EditorUtility.DisplayProgressBar("Downloading AssetRipper DLL", $"Downloading from {dllUrl}", args.ProgressPercentage / 100f);
                    };
                    await client.DownloadFileTaskAsync(dllUrl, zipLocation);
                }
                
                EditorUtility.ClearProgressBar();
                
                if (!File.Exists(zipLocation)) {
                    throw new Exception("Failed to download AssetRipper DLL");
                }
                
                Debug.Log($"Extracting {zipLocation} to {dllLocation}");
                
                try {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipLocation, Path.GetDirectoryName(dllLocation));
                } catch (Exception e) {
                    Debug.LogError(e);
                    throw;
                }

                try {
                    File.Delete(zipLocation);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
                
                if (!File.Exists(dllLocation)) {
                    throw new Exception("Failed to extract AssetRipper DLL");
                }
            }

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
                    RedirectStandardError = true,
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
                
                var errorOutput = process.StandardError.ReadToEnd();

                // check for any errors
                if (process.ExitCode != 0) {
                    throw new Exception($"AssetRipper failed to run with exit code {process.ExitCode}. Error: {errorOutput}");
                }
                
                // ? copy the files from the Temp folder into a temp folder in the project
                // ModuleUtility.CopyFilesRecursively(outputPath, ModuleUtility.AssetRipperTempDirectory);
            } catch (Exception e) {
                Debug.LogError($"Error running AssetRipper: {e}");
                throw;
            }
        }

        public static bool HasDunGenAsset {
            get {
                var settings = ModuleUtility.GetPatcherSettings();
                var nativePath = settings.GetAssetStorePath(fullPath: true);
                var assetDunGenPath = Path.Combine(nativePath, "DunGen");
                return Directory.Exists(assetDunGenPath);
            }
        }

        public static void RemoveDunGenFromOutputIfNeeded(LCPatcherSettings settings) {
            // ? check if we have DunGen in the project already
            var nativePath = settings.GetAssetStorePath(fullPath: true);
            var assetDunGenPath = Path.Combine(nativePath, "DunGen");
            if (!HasDunGenAsset) {
                Debug.Log($"DunGen not found at {assetDunGenPath}");
                return;
            }

            // remove DunGen from the Asset Ripper output
            var assetRipperDunGenPath = Path.Combine(ModuleUtility.AssetRipperTempDirectoryExportedProject, "Assets", "Scripts", "Assembly-CSharp", "DunGen");
            Debug.Log($"Removing DunGen from AssetRipper output at {assetRipperDunGenPath}");
            if (Directory.Exists(assetRipperDunGenPath)) {
                Directory.Delete(assetRipperDunGenPath, recursive: true);
            }
            
            // remove DunGen from the project folder
            var projectGamePath = settings.GetLethalCompanyGamePath(fullPath: true);
            string scriptsPath;
            if (settings.AssetRipperSettings.TryGetMapping("Scripts", out var finalFolder)) {
                scriptsPath = Path.Combine(projectGamePath, finalFolder);
            } else {
                scriptsPath = Path.Combine(projectGamePath, "Scripts");
            }
            var projectDunGenPath = Path.Combine(scriptsPath, "Assembly-CSharp", "DunGen");
            Debug.Log($"Removing DunGen from project at {projectDunGenPath}");
            if (Directory.Exists(projectDunGenPath)) {
                Directory.Delete(projectDunGenPath, recursive: true);
            }

            // import the navmesh package from the asset
            EditorUtility.DisplayProgressBar("Installing packages", "Installing DunGen NavMesh package", 0.75f);
            var packagepath = Path.Combine(settings.GetAssetStorePath(), "DunGen", "Integration", "Unity NavMesh.unitypackage");
            Debug.Log($"Importing package from {packagepath}");
            AssetDatabase.ImportPackage(packagepath, false);
            EditorUtility.ClearProgressBar();
        }
        
        public static void CopyAssetRipperContents(LCPatcherSettings settings) {
            var assetRipperSettings = settings.AssetRipperSettings;
            var outputRootFolder = settings.GetLethalCompanyGamePath();
            
            var assetRipperTempFolder = ModuleUtility.AssetRipperTempDirectoryExportedProject;
            var assetsFolder = Path.Combine(assetRipperTempFolder, "Assets");
            
            var folders = Directory.GetDirectories(assetsFolder);
            foreach (var folder in folders) {
                var folderName = Path.GetFileName(folder);
                if (folderName == "Scripts" || folderName == "Shader") {
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
    }
}
