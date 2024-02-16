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
        // TODO: Replace this so all custom shaders are automatically replaced, instead of needing a hardcoded list
        public static readonly List<string> ShadersToGrab = new() {
            "Shader Graphs/PosterizationFilter",
            "Shader Graphs/WaterShaderHDRP",
            "Shader Graphs/BlobShader",
            "Shader Graphs/HologramShader",
            // "Hidden/VFX/FlyingBugs/System/Output Particle HDRP Lit Mesh" // disabled as-is because the VisualEffect is broken anyways
        };
        
        private static List<string> _loadedAssetsFilePaths = new();

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

            // get all project materials/shaders to filter against later
            var materials = AssetDatabase.FindAssets("t:material")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<Material>(path))
                .ToList();
            var shaders = AssetDatabase.FindAssets("t:shader")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<Shader>(path))
                .ToList();
                
            List<ShaderInjection> shaderInjections = new();
            
            for (int i = 0; i < ShadersToGrab.Count; i++) {
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

                shaderInjections.Add(GetShaderInjection(shaderToGrab, shortShaderName, materials, shaders));
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

        private static ShaderInjection GetShaderInjection(string shaderName, string bundleName, List<Material> materials, List<Shader> shaders) {
            var filteredMaterials = materials.Where(x => x.shader.name == shaderName).ToList();
            var filteredShaders = shaders.Where(x => x.name == shaderName).ToList();
            
            var shaderInjection = new ShaderInjection {
                ShaderName = shaderName,
                BundleName = bundleName,
                DummyShaders = filteredShaders,
                Materials = filteredMaterials
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
            
            // Inject the actual shader
            var (shaderInfo, shaderData) = GetFirstAssetInfoAndBaseOfClassID(assetsFileInstance, AssetClassID.Shader, assetsManager);
            var newPathId = shaderInfo.PathId - (index + 1) * 20; // prevent path id overlaps from other assets

            // Get AssetBundle asset
            var (assetBundleInfo, assetBundleData) = GetFirstAssetInfoAndBaseOfClassID(assetsFileInstance, AssetClassID.AssetBundle, assetsManager);
            
            // Force set AssetBundle name & paths to line up
            // If you try to load an AssetBundle with the same name/assets twice, it will refuse to load
            assetBundleData["m_Name"].AsString = shaderName;
            assetBundleData["m_AssetBundleName"].AsString = shaderName;
            
            // Replace name with a fake "assets" path that won't conflict with anything
            assetBundleData["m_Container.Array"].Children[0]["first"].AsString = $"assets/injectedshaders/{shaderName}.shader";
            
            // Replace m_Container (assetbundle internal object array) with our new Path ID
            assetBundleData["m_Container.Array"].Children[0]["second"]["asset"]["m_PathID"].AsLong = newPathId;
            
            // Remap the preload table pptrs. Only this method worked. I have no idea why.
            RemapPPtrs(assetBundleData["m_PreloadTable.Array"], new Dictionary<(int fileId, long pathId), (int fileId, long pathId)> {
                {(0, shaderInfo.PathId), (0, newPathId)}
            });
            
            // Remap shader dependencies. I pray there is never more than the shader graph fallback error
            // This just destroys the dependency array for now. That's probably fine, right?
            // Needed to avoid "illegal LocalPathID in PersistentManager" error
            shader["m_Dependencies.Array"].Children = new List<AssetTypeValueField>();

            // Finally, set shader iD and our new data
            shaderInfo.PathId = newPathId; 
            shaderInfo.SetNewData(shader);
            assetBundleInfo.SetNewData(assetBundleData);

            // Overwrite AssetsFile in bundle, then write everything to the new path
            bundleFileInstance.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assetsFile);
            using AssetsFileWriter writer = new AssetsFileWriter(newBundlePath);
            bundleFileInstance.file.Write(writer);
        }

        // https://github.com/PassivePicasso/BundleKit/blob/0b53bdf51b968094a3aa753f695237d13a97f649/Editor/Utility/AssetsToolsExtensions.cs#L179
        public static void RemapPPtrs(this AssetTypeValueField field, IDictionary<(int fileId, long pathId), (int fileId, long pathId)> map) {
            var fieldStack = new Stack<AssetTypeValueField>();
            fieldStack.Push(field);
            while (fieldStack.Any()) {
                var current = fieldStack.Pop();
                foreach (AssetTypeValueField child in current.Children) {
                    //not a value (ie not an int)
                    if (!child.TemplateField.HasValue) {
                        //not array of values either
                        if (child.TemplateField.IsArray && child.TemplateField.Children[1].ValueType != AssetValueType.None) {
                            continue;
                        }

                        string typeName = child.TemplateField.Type;
                        //is a pptr
                        if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">")) {
                            var fileIdField = child.Get("m_FileID").Value;
                            var pathIdField = child.Get("m_PathID").Value;
                            var pathId = pathIdField.AsLong;
                            var fileId = fileIdField.AsInt;
                            if (!map.ContainsKey((fileId, pathId))) {
                                continue;
                            }
                            var newPPtr = map[(fileId, pathId)];
                            fileIdField.AsInt = newPPtr.fileId;
                            pathIdField.AsLong = newPPtr.pathId;
                        }

                        //recurse through dependencies
                        fieldStack.Push(child);
                    }
                }
            }
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