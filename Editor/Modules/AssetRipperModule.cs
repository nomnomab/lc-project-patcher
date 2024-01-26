using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcherScriptCleaner;
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
            "Scenes",
            "Sprite",
            "TerrainData",
            "TerrainLayer",
            "Texture2D",
            "Texture3D",
            "VideoClip"
        };

        public static UniTask PatchInstall() {
            var assetRipperPath = EditorPrefs.GetString("nomnom.lc_project_patcher.asset_ripper_path");
            for (var i = 0; i < InitialFolders.Length; i++) {
                var folder = InitialFolders[i];
                var ripperPath = Path.Combine(assetRipperPath, "Assets", folder);
                var projectPath = Path.Combine(Application.dataPath, folder);
                EditorUtility.DisplayProgressBar("Copying AssetRipper files", $"Copying {folder} to {projectPath}", (float)i / InitialFolders.Length);
                
                Debug.Log($"Copying {ripperPath} to {projectPath}");
                ModuleUtility.CopyFilesRecursively(ripperPath, projectPath);
            }

            // copy all files and folders from Assembly-CSharp
            var assetRipperAssemblyCSharpPath = Path.Combine(assetRipperPath, "Assets", "Scripts", "Assembly-CSharp");
            var projectAssemblyCSharpPath = Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp");
            
            Debug.Log($"Copying {assetRipperAssemblyCSharpPath} to {projectAssemblyCSharpPath}");
            ModuleUtility.CopyFilesRecursively(assetRipperAssemblyCSharpPath, projectAssemblyCSharpPath);
            
            EditorUtility.DisplayProgressBar("Scrubbing Assembly-CSharp", "Scrubbing Assembly-CSharp", 0.5f);
            ScriptScrubber.Scrub(Directory.GetFiles(projectAssemblyCSharpPath, "*.cs", SearchOption.AllDirectories));
            EditorUtility.ClearProgressBar();
            
            // check if we have DunGen in the project already
            var dungenPath = Path.Combine(Application.dataPath, "DunGen");
            if (Directory.Exists(dungenPath)) {
                // remove DunGen from the stubs
                var stubsPath = Path.Combine(projectAssemblyCSharpPath, "DunGen");
                Directory.Delete(stubsPath, recursive: true);
                
                // import the navmesh package from it
                EditorUtility.DisplayProgressBar("Installing packages", "Installing DunGen NavMesh package", 0.75f);
                AssetDatabase.ImportPackage("Assets/DunGen/Integration/Unity NavMesh.unitypackage", false);
                EditorUtility.ClearProgressBar();
            }
            
            return UniTask.CompletedTask;
        }

        public static UniTask PatchFix() {
            var assetRipperPath = EditorPrefs.GetString("nomnom.lc_project_patcher.asset_ripper_path");
            
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
            
            EditorUtility.ClearProgressBar();

            return UniTask.CompletedTask;
        }
    }
}
