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
        public static readonly string ShaderInjectionSettingsPath = "Assets/Resources/ShaderInjectionSettings.asset";
        public static readonly List<string> ShadersToGrab = new() {
            "Shader Graphs/PosterizationFilter",
            "Shader Graphs/WaterShaderHDRP",
        };
        
        private static List<string> _loadedAssetsFilePaths = new();
        
        [MenuItem("Tools/Nomnom/ShaderInjection", priority = 1)]
        public static void Thing() {
            Debug.Log("Oh no.");
            var shaderInjectionSettings = Resources.Load<LCPatcherShaderInjectionSettings>("ShaderInjectionSettings");
            
            foreach (var shaderInjection in shaderInjectionSettings.ShaderInjections) {
                Debug.Log(shaderInjection.ShaderName);
                // Load bundle
                var bundlePath = Path.Join(Application.streamingAssetsPath, "ShaderInjections",
                    $"{shaderInjection.BundleName}.shaderinject");
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                var shader = bundle.LoadAsset<Shader>($"assets/injectedshaders/{shaderInjection.BundleName}.shader");

                // Set material shaders
                foreach (var material in shaderInjection.Materials) {
                    material.shader = shader;
                }
                
                // Unload
                bundle.Unload(false);
            }
        }
        
        public static void GetShaders(LCPatcherSettings settings) {
            AssetsManager assetsManager = new();
            
            // clear loaded paths
            _loadedAssetsFilePaths.Clear();
            
            // Load Unity type tree file so we can actually use our game's asset files
            var classPackagePath = Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/AssetsTools.NET/uncompressed.tpk");
            assetsManager.LoadClassPackage(classPackagePath);
            
            // Get assets files and attempt to find our shaders
            var assetsFileInstances = LoadAssetsFilesFromDataPath(ModuleUtility.GameDataPath, assetsManager);

            // create dummy shader bundle. not sure if there's a way to create a 100% new assetbundle using AssetsTools.net
            AssetBundleModule.CreateShaderBundle("dummy");
            
            // setup resources folder for shader bundles
            var shaderDirectory = Path.Join(settings.GetStreamingAssetsPath(true), "ShaderInjections");
            Directory.CreateDirectory(shaderDirectory);

            List<ShaderInjection> shaderInjections = new();
            
            for (int i = 0; i <ShadersToGrab.Count; i++) {
                var shaderToGrab = ShadersToGrab[i];
                var shader = GetShaderFromAssetsFiles(shaderToGrab, assetsFileInstances, assetsManager);
                if (shader == null) {
                    continue;
                }

                // may need better parsing if more complex shader names are ever filtered against
                var shortShaderName = shaderToGrab.Split("/").Last().Replace(" ", "").ToLower();
                InjectShaderIntoExistingAssetBundle(
                    Path.Join(Application.temporaryCachePath, "dummy"),
                    Path.Join(shaderDirectory, $"{shortShaderName}.shaderinject"),
                    shortShaderName,
                    i,
                    shader,
                    assetsManager);

                shaderInjections.Add(GetShaderInjection(shaderToGrab, shortShaderName));
            }

            // Create shaderInjection SO
            var injectionSettings = ScriptableObject.CreateInstance<LCPatcherShaderInjectionSettings>();
            injectionSettings.ShaderInjections = shaderInjections;
            injectionSettings.EnableShaderInjections = true;
            
            // TODO: User-facing warning this is destructive or something?
            if (AssetDatabase.FindAssets(ShaderInjectionSettingsPath).Length > 0) {
                AssetDatabase.DeleteAsset(ShaderInjectionSettingsPath);
            }
            AssetDatabase.CreateAsset(injectionSettings, ShaderInjectionSettingsPath);
            
            // Get rid of progress bar from loading the asset files
            EditorUtility.ClearProgressBar();
            
            // Unload
            assetsManager.UnloadAll();
        }

        private static ShaderInjection GetShaderInjection(string shaderName, string bundleName) {
            var dummyShader = Shader.Find(shaderName);
            var materials = AssetDatabase.FindAssets("t:material")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<Material>(path))
                .Where(x => x.shader.name == shaderName)
                .ToList();
            
            var shaderInjection = new ShaderInjection {
                ShaderName = shaderName,
                BundleName = bundleName,
                DummyShader = dummyShader,
                Materials = materials
            };

            return shaderInjection;
        }

        private static void InjectShaderIntoExistingAssetBundle(string currentBundlePath, string newBundlePath, string shaderName, int index, AssetTypeValueField shader, AssetsManager assetsManager) {
            // I'm not sure if we can easily create fully new AssetBundles using only AssetsTools.NET
            // If we can, refactor this to use CreateValueBaseField to add the actual shader in a brand new assetbundle
            var bundleFileInstance = assetsManager.LoadBundleFile(currentBundlePath);
            var assetsFileInstance = assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, 0);
            var assetsFile = assetsFileInstance?.file;
            if (assetsFileInstance == null || assetsFile == null) {
                throw new Exception($"Could not load bundle file at {currentBundlePath}");
            }

            // Load unity version so we can get the correct class database setup
            string unityVersion = assetsFile.Metadata.UnityVersion;
            assetsManager.LoadClassDatabaseFromPackage(unityVersion);
            
            // Get actual AssetBundle asset
            var (assetBundleInfo, assetBundleData) = GetFirstAssetInfoAndBaseOfClassID(assetsFileInstance, AssetClassID.AssetBundle, assetsManager);
            
            // Force set AssetBundle name & paths to line up
            // If you try to load an AssetBundle with the same name/assets twice, it will refuse to load
            assetBundleData["m_Name"].AsString = shaderName;
            assetBundleData["m_AssetBundleName"].AsString = shaderName;
            assetBundleData["m_Container.Array"].Children[0]["first"].AsString = $"assets/injectedshaders/{shaderName}.shader"; // ???
            assetBundleInfo.SetNewData(assetBundleData);
            
            // Inject the actual shader
            var (shaderInfo, shaderData) = GetFirstAssetInfoAndBaseOfClassID(assetsFileInstance, AssetClassID.Shader, assetsManager);
            shaderInfo.SetNewData(shader);
            shaderInfo.PathId -= (index + 1) * 20; // prevent path id overlaps
            // Overwrite AssetsFile in bundle, then write everything to the new path
            bundleFileInstance.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assetsFile);
            using AssetsFileWriter writer = new AssetsFileWriter(newBundlePath);
            bundleFileInstance.file.Write(writer);
        }

        [CanBeNull]
        private static AssetTypeValueField GetShaderFromAssetsFiles(string requiredShaderName, List<AssetsFileInstance> assetsFileInstances, AssetsManager assetsManager) {
            for (int i = 0; i < assetsFileInstances.Count; i++) {
                var assetsFileInstance = assetsFileInstances[i];
                EditorUtility.DisplayProgressBar("Extracting shaders", $"Extracting from {assetsFileInstance.name}", (float)i / assetsFileInstances.Count);
                
                var shader = GetAssetInfoAndBaseOfClassID(assetsFileInstance, AssetClassID.Shader, assetsManager)
                    .Select(x => x.assetBase)
                    .FirstOrDefault(x => x["m_ParsedForm"]["m_Name"].AsString == requiredShaderName);
                if (shader != null) {
                    return shader;
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
            string unityVersion = assetsFile.Metadata.UnityVersion;
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

        // handles all the annoying null checks for assetfile/assetdata
        [CanBeNull]
        private static (AssetFileInfo assetInfo, AssetTypeValueField assetBase) GetFirstAssetInfoAndBaseOfClassID(AssetsFileInstance assetsFileInstance, AssetClassID assetClassID, AssetsManager assetsManager) {
            var assetsInfoAndData = GetAssetInfoAndBaseOfClassID(assetsFileInstance, assetClassID, assetsManager);
            var assetInfoAndData = assetsInfoAndData?.FirstOrDefault();
            if (assetInfoAndData == null) {
                throw new Exception($"Could not find asset of class {assetClassID} in assetsFileInstance.");
            }
            return assetInfoAndData!.Value;
        } 
        
        // handles all the annoying null checks for assetfile/assetdata
        private static List<(AssetFileInfo assetInfo, AssetTypeValueField assetBase)> GetAssetInfoAndBaseOfClassID(AssetsFileInstance assetsFileInstance, AssetClassID assetClassID, AssetsManager assetsManager) {
            var assetsFile = assetsFileInstance.file;
            if (assetsFile == null) {
                return new();
            }
            List<(AssetFileInfo, AssetTypeValueField)> assetsInfoAndData = new();
            foreach (var assetInfo in assetsFile.GetAssetsOfType(assetClassID)) {
                if (assetInfo == null) {
                    continue;
                }
                var assetBase = assetsManager.GetBaseField(assetsFileInstance, assetInfo);
                if (assetBase == null) {
                    continue;
                }
                assetsInfoAndData.Add((assetInfo, assetBase));
            }

            return assetsInfoAndData;
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