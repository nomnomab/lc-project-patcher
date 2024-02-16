using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

// This may be redundant for AssetRipper, but I'm not familiar enough with AssetRipper's codebase to make it do this
namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class AssetsToolsModule {
        private static List<string> _loadedAssetsFilePaths = new();
        private static readonly string _shaderString = "Shader Graphs/PosterizationFilter";
    
        public static void GetShader(LCPatcherSettings settings) {
            AssetsManager assetsManager = new();
            
            // clear loaded paths
            _loadedAssetsFilePaths.Clear();
            
            // Load Unity type tree file so we can actually use our game's asset files
            var classPackagePath = Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/AssetsTools.NET/uncompressed.tpk");
            assetsManager.LoadClassPackage(classPackagePath);
            
            // Get assets files and attempt to find our shaders
            var assetsFileInstances = LoadAssetsFilesFromDataPath(ModuleUtility.GameDataPath, assetsManager);
            var shader = GetShaderFromAssetsFiles(_shaderString, assetsFileInstances, assetsManager);

            Debug.Log(shader);
            
            // Unload
            assetsManager.UnloadAll();
        }

        [CanBeNull]
        private static AssetTypeValueField GetShaderFromAssetsFiles(string requiredShaderName, List<AssetsFileInstance> assetsFileInstances, AssetsManager assetsManager) {
            for (int i = 0; i < assetsFileInstances.Count; i++) {
                var assetsFileInstance = assetsFileInstances[i];
                EditorUtility.DisplayProgressBar("Extracting shaders", $"Extracting from {assetsFileInstance.name}", (float)i / assetsFileInstances.Count);
                
                var assetsFile = assetsFileInstance.file;
                foreach (var shaderInfo in assetsFile.GetAssetsOfType(AssetClassID.Shader)) {
                    var shaderBase = assetsManager.GetBaseField(assetsFileInstance, shaderInfo);
                    if (shaderBase == null) {
                        continue;
                    }
                    var shaderName = shaderBase["m_ParsedForm"]["m_Name"].AsString;
                    if (shaderName == requiredShaderName) {
                        return shaderBase;
                    }
                }
            }

            return null;
        }
        
        // Load an assets file, and any non-loaded dependencies
        private static List<AssetsFileInstance> RecursivelyLoadAssetsFile(string assetsFilePath, AssetsManager assetsManager, int? depth = 0) {
            if (_loadedAssetsFilePaths.Contains(assetsFilePath) || depth > 50) {
                return new();
            }
            _loadedAssetsFilePaths.Add(assetsFilePath);

            // Attempt to recursively load all assetsfiles and dependents
            List<AssetsFileInstance> assetsFileInstances = new();

            var assetsInstance = assetsManager.LoadAssetsFile(assetsFilePath, true);
            var assetsFile = assetsInstance.file;
            assetsFile.GenerateQuickLookup(); // not 100% sure what this does

            if (assetsInstance == null || assetsFile == null) {
                throw new Exception($"Could not load assets file at {assetsFilePath}");
            }

            // Load unity version so we can get the correct class database setup
            string unityVersion = assetsFile?.Metadata.UnityVersion;
            assetsManager.LoadClassDatabaseFromPackage(unityVersion);

            for (int i = 0; i < assetsFile.Metadata.Externals.Count; i++) {
                AssetsFileInstance dependency = assetsInstance.GetDependency(assetsManager, i);
                if (dependency == null) {
                    continue;
                }

                string dependencyPath = dependency.path.ToLower();
                if (!_loadedAssetsFilePaths.Contains(dependencyPath)) {
                    assetsFileInstances.AddRange(RecursivelyLoadAssetsFile(dependencyPath, assetsManager, depth + 1));
                }
            }

            assetsFileInstances.Add(assetsInstance);
            return assetsFileInstances;
        }
        
        private static List<AssetsFileInstance> LoadAssetsFilesFromDataPath(string dataPath, AssetsManager assetsManager) {
            List<AssetsFileInstance> assetsFileInstances = new();

            foreach (var assetsFilePath in GetAssetsFilePathsFromDataPath(dataPath)) {
                assetsFileInstances.AddRange(RecursivelyLoadAssetsFile(assetsFilePath, assetsManager));
            }

            return assetsFileInstances;
        }

        private static IEnumerable<string> GetAssetsFilePathsFromDataPath(string dataPath) {
            if (!Directory.Exists(dataPath)) {
                throw new DirectoryNotFoundException("Could not find data folder");
            }

            List<string> assetsFilePaths = new();
            
            foreach (var file in Directory.GetFiles(dataPath)) {
                var fileName = Path.GetFileName(file);
                if (Path.GetExtension(file) != ".assets") {
                    continue;
                }
                // sharedassets *may* be unnecessary, not sure if all the game's assets are in the resources folder
                if (!fileName.StartsWith("sharedassets") && !fileName.StartsWith("resources")) {
                    continue;
                }
                assetsFilePaths.Add(file);
            }

            return assetsFilePaths;
        }
    }
}