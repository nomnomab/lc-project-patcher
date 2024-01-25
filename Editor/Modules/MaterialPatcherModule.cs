// using System.IO;
// using System.Linq;
// using System.Text;
// using Cysharp.Threading.Tasks;
// using UnityEditor;
// using UnityEngine;
//
// namespace Nomnom.LCProjectPatcher.Modules {
//     public static class MaterialPatcherModule {
//         public static async UniTask Patch() {
//             var internalShaders = AssetDatabase.FindAssets("t:Shader", new[] { "Packages/com.unity.render-pipelines.high-definition", "Assets/TextMesh Pro" })
//                 .Select(AssetDatabase.GUIDToAssetPath)
//                 .Select(AssetDatabase.LoadAssetAtPath<Shader>)
//                 .Where(x => x.name.StartsWith("HDRP") || x.name.StartsWith("TextMeshPro"))
//                 .ToArray();
//             Debug.Log($"Found {internalShaders.Length} internal shaders");
//
//             var assetRipperPath = EditorPrefs.GetString("nomnom.lc_project_patcher.asset_ripper_path");
//             var rootPath = Application.dataPath;
//             var materialsPath = Path.Combine(rootPath, "Material");
//             var materialFiles = Directory.GetFiles(materialsPath, "*.mat", SearchOption.AllDirectories);
//             Debug.Log($"Found {materialFiles.Length} materials");
//             foreach (var (file, guid) in AssetRipperUtilities.ScanAssetRipperProjectUnknown(assetRipperPath, "Shader", "*.shader")) {
//                 var fileName = Path.GetFileNameWithoutExtension(file);
//                 if (!(fileName.StartsWith("HDRP") || fileName.StartsWith("TextMeshPro"))) {
//                     continue;
//                 }
//
//                 // now we need the shader's path
//                 var fileContents = File.ReadAllText(file);
//                 var shaderPath = AssetRipperUtilities.GetShaderPathFromShader(fileContents);
//                 Debug.Log($"{fileName} ({guid}) at \"{shaderPath}\"");
//
//                 var internalShader = internalShaders.FirstOrDefault(x => x.name == shaderPath);
//                 if (internalShader == null) {
//                     Debug.LogWarning($"Could not find internal shader for {shaderPath}");
//                     continue;
//                 }
//
//                 var newGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(internalShader));
//                 Debug.Log($"- {internalShader.name} ({newGuid})");
//
//                 // now find all materials in this project that use the original guid
//                 foreach (var materialFile in materialFiles) {
//                     var materialFileContents = File.ReadAllText(materialFile);
//                     if (!materialFileContents.Contains(guid)) {
//                         continue;
//                     }
//
//                     Debug.Log($"- Found {Path.GetFileName(materialFile)} with {guid}, replacing with {newGuid}");
//
//                     // now we need to replace the guid with the internal shader's guid
//                     var sb = new StringBuilder(materialFileContents);
//                     sb.Replace(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(internalShader)));
//                     File.WriteAllText(materialFile, sb.ToString());
//                 }
//             }
//         }
//     }
// }
