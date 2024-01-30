using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Modules {
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

        public static UniTask PatchInstall() {
            var assetRipperPath = ModuleUtility.GetAssetRipperCloneDirectory();
            for (var i = 0; i < InitialFolders.Length; i++) {
                var folder = InitialFolders[i];
                var ripperPath = Path.Combine(assetRipperPath, "Assets", folder);
                var projectPath = Path.Combine(Application.dataPath, folder);
                EditorUtility.DisplayProgressBar("Copying AssetRipper files", $"Copying {folder} to {projectPath}", (float)i / InitialFolders.Length);
                
                Debug.Log($"Copying {ripperPath} to {projectPath}");
                ModuleUtility.CopyFilesRecursively(ripperPath, projectPath);
            }
            
            // create ES3Defaults.cs in Resources/es3
            var es3DefaultsFormat = Resources.Load<TextAsset>("WrapperScriptTemplate").text;
            var es3DefaultsPath = Path.Combine(Application.dataPath, "Scripts", "Resources", "es3");
            Directory.CreateDirectory(es3DefaultsPath);
            
            es3DefaultsFormat = es3DefaultsFormat
                .Replace("$CLASS_NAME$", "ES3Defaults")
                .Replace("$BASE_CLASS$", "global::ES3Defaults");
            
            File.WriteAllText(Path.Combine(es3DefaultsPath, "ES3Defaults.cs"), es3DefaultsFormat);
            
            // EditorUtility.DisplayProgressBar("Cloning scripts", "Cloning scripts", 0.8f);
            //
            // // create lethal-company.asmdef
            // var lethalCompanyAsmDefFormat = Resources.Load<TextAsset>("LethalCompanyAsmdefTemplate").text;
            // var lethalCompanyAsmDefPath = Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp");
            // Debug.Log($"Creating {lethalCompanyAsmDefPath}");
            //
            // Directory.CreateDirectory(lethalCompanyAsmDefPath);
            // File.WriteAllText(Path.Combine(lethalCompanyAsmDefPath, "lethal-company.asmdef"), lethalCompanyAsmDefFormat);
            //
            // // create lethal-company-firstpass.asmdef
            // var lethalCompanyFirstPassAsmDefFormat = Resources.Load<TextAsset>("LethalCompanyFirstPassAsmdefTemplate").text;
            // var lethalCompanyFirstPassAsmDefPath = Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp-firstpass");
            // Debug.Log($"Creating {lethalCompanyFirstPassAsmDefPath}");
            //
            // Directory.CreateDirectory(lethalCompanyFirstPassAsmDefPath);
            // File.WriteAllText(Path.Combine(lethalCompanyFirstPassAsmDefPath, "lethal-company-firstpass.asmdef"), lethalCompanyFirstPassAsmDefFormat);
            //
            // // create ES3Defaults.cs
            // var es3DefaultsFormat = Resources.Load<TextAsset>("WrapperScriptTemplate").text;
            // Directory.CreateDirectory(lethalCompanyFirstPassAsmDefPath);
            // es3DefaultsFormat = es3DefaultsFormat
            //     .Replace("$CLASS_NAME$", "ES3Defaults")
            //     .Replace("$BASE_CLASS$", "global::ES3Defaults");
            // File.WriteAllText(Path.Combine(lethalCompanyFirstPassAsmDefPath, "ES3Defaults.cs"), es3DefaultsFormat);
            //
            // // go through all scripts in Assembly-CSharp
            // var scriptFormat = Resources.Load<TextAsset>("WrapperScriptTemplate").text;
            // var assetRipperAssemblyCSharpPath = Path.Combine(assetRipperPath, "Assets", "Scripts", "Assembly-CSharp");
            // var files = Directory.GetFiles(assetRipperAssemblyCSharpPath, "*.cs", SearchOption.AllDirectories);
            //
            // var assembliesFromPluginsFolder = Directory
            //     .GetFiles(Path.Combine(Application.dataPath, "Plugins"), "*.dll", SearchOption.TopDirectoryOnly)
            //     .ToArray();
            // var unityAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            //     .Where(x => x.FullName.StartsWith("UnityEngine"))
            //     .Select(x => x.Location)
            //     .ToArray();
            // var assemblies = assembliesFromPluginsFolder
            //     .Concat(unityAssemblies)
            //     // .Select(x => x.Replace('/', Path.DirectorySeparatorChar))
            //     .ToArray();
            //
            // var usedFiles = ScriptCloner.Clone(assetRipperPath, Application.dataPath, files, scriptFormat, Debug.Log, assemblies);
            // var unusedFiles = files.Except(usedFiles)
            //     .Select(x => (x, x.Replace(Path.Combine(assetRipperPath, "Assets"), Application.dataPath)))
            //     .ToArray();
            // for (var i = 0; i < unusedFiles.Length; i++) {
            //     var (file, newFile) = unusedFiles[i];
            //     if (!File.Exists(file)) {
            //         continue;
            //     }
            //     
            //     newFile = newFile.Replace("Assembly-CSharp", "Assembly-CSharp__Stripped");
            //     
            //     var directory = Path.GetDirectoryName(newFile);
            //     if (directory == null) {
            //         continue;
            //     }
            //     
            //     Directory.CreateDirectory(directory);
            //     File.Copy(file, newFile, overwrite: true);
            // }
            // EditorUtility.ClearProgressBar();

            // copy all files and folders from Assembly-CSharp
            // var assetRipperAssemblyCSharpPath = Path.Combine(assetRipperPath, "Assets", "Scripts", "Assembly-CSharp");
            
            // Debug.Log($"Copying {assetRipperAssemblyCSharpPath} to {projectAssemblyCSharpPath}");
            // ModuleUtility.CopyFilesRecursively(assetRipperAssemblyCSharpPath, projectAssemblyCSharpPath);
            
            // EditorUtility.DisplayProgressBar("Scrubbing Assembly-CSharp", "Scrubbing Assembly-CSharp", 0.5f);
            // // ScriptScrubber.Scrub(Directory.GetFiles(projectAssemblyCSharpPath, "*.cs", SearchOption.AllDirectories));
            // ScriptScrubber.Scrub(unusedFiles.Select(x => x.Item2.Replace("Assembly-CSharp", "Assembly-CSharp__Stripped")).ToArray(), Debug.Log);
            // EditorUtility.ClearProgressBar();
            
            // check if we have DunGen in the project already
            var dungenPath = Path.Combine(Application.dataPath, "DunGen");
            if (Directory.Exists(dungenPath)) {
                // remove DunGen from the stubs
                var projectAssemblyCSharpPath = Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp");
                var stubsPath = Path.Combine(projectAssemblyCSharpPath, "DunGen");
                Directory.Delete(stubsPath, recursive: true);
                
                // import the navmesh package from it
                EditorUtility.DisplayProgressBar("Installing packages", "Installing DunGen NavMesh package", 0.75f);
                AssetDatabase.ImportPackage("Assets/DunGen/Integration/Unity NavMesh.unitypackage", false);
                EditorUtility.ClearProgressBar();
            }
            
            Debug.Log("AssetRipper files copied");
            
            return UniTask.CompletedTask;
        }

        public static UniTask PatchFix() {
            var assetRipperPath = ModuleUtility.GetAssetRipperCloneDirectory();
            
            // copy shadergraph shaders
            var shaderGraphPath = Path.Combine(assetRipperPath, "Assets", "Shader");
            var projectShaderGraphPath = Path.Combine(Application.dataPath, "Shader");
            Directory.CreateDirectory(projectShaderGraphPath);

            var shaderGraphFiles = Directory
                .GetFiles(shaderGraphPath, "*.shader", SearchOption.AllDirectories)
                .Where(x => Path.GetFileNameWithoutExtension(x).StartsWith("Shader Graphs"))
                .ToArray();

            for (var i = 0; i < shaderGraphFiles.Length; i++) {
                var file = shaderGraphFiles[i];
                var fileName = Path.GetFileName(file);
                var projectFilePath = Path.Combine(projectShaderGraphPath, fileName);
                EditorUtility.DisplayProgressBar("Copying Shader Graphs", $"Copying {fileName} to {projectFilePath}", (float)i / shaderGraphFiles.Length);
                File.Copy(file, projectFilePath, overwrite: true);
            }

            for (var i = 0; i < FinalFolders.Length; i++) {
                var folder = FinalFolders[i];
                var ripperPath = Path.Combine(assetRipperPath, "Assets", folder);
                var projectPath = Path.Combine(Application.dataPath, folder);
                EditorUtility.DisplayProgressBar("Copying AssetRipper files", $"Copying {folder} to {projectPath}", (float)i / FinalFolders.Length);
                
                ModuleUtility.CopyFilesRecursively(ripperPath, projectPath);
                Debug.Log($"Copying {ripperPath} to {projectPath}");
            }
            
            // fix es3 defaults manually in Assets/Resources/es3/ES3Defaults.asset
            var filePath = Path.Combine(Application.dataPath, "Resources", "es3", "ES3Defaults.asset");
            var fileContents = File.ReadAllText(filePath);
            var match = GuidPatcherModule.GuidPattern.Match(fileContents);
            if (!match.Success) {
                throw new Exception("Could not find guid for ES3Defaults");
            }
            
            var es3Script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Resources/es3/ES3Defaults.cs");
            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(es3Script);
            var assetGuid = globalId.assetGUID.ToString();
            var objectId = globalId.targetObjectId;
            Debug.Log($"Found ES3Defaults | {assetGuid} | {objectId}");
            fileContents = GuidPatcherModule.GuidPattern.Replace(fileContents, $"guid: {assetGuid}");
            Debug.Log($"Fixed ES3Defaults at {filePath} to {fileContents}");
                    
            File.WriteAllText(filePath, fileContents);
            
            EditorUtility.ClearProgressBar();

            return UniTask.CompletedTask;
        }
    }
}
