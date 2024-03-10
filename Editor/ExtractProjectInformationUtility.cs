using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nomnom.LCProjectPatcher.Editor.Modules;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher.Editor {
    public static class ExtractProjectInformationUtility {
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Extract Project Information")]
        public static ExtractedResults ExtractProjectInformation() {
            var outputFilePath = EditorUtility.SaveFilePanel("Save Project Information", "", "ProjectInformation", "json");
            if (string.IsNullOrEmpty(outputFilePath)) {
                return default;
            }

            var results = CreateExtractedResults(true);
            var json = JsonUtility.ToJson(results, true);
            System.IO.File.WriteAllText(outputFilePath, json);
            
            Debug.Log($"Extracted {results.guids.Length} guids to {outputFilePath}");
            EditorUtility.RevealInFinder(outputFilePath);
            return results;
        }

        public static ExtractedResults CreateExtractedResults(bool onlyInProjectAssets) {
            Debug.Log("Extracting project information...");
            var scriptGuids = GetScriptResults(onlyInProjectAssets);
            var assetGuids = GetAssetResults(onlyInProjectAssets);
            var results = new ExtractedResults();
            results.guids = scriptGuids.ToArray();
            results.assetGuids = assetGuids.ToArray();
            return results;
        }

        private static IEnumerable<GuidResult> GetScriptResults(bool onlyInProjectAssets) {
            var allMonoScripts = !onlyInProjectAssets
                ? AssetDatabase.FindAssets("t:MonoScript")
                // : AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/LethalCompany" });
                : AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            var badGuids = 0;
            for (var i = 0; i < allMonoScripts.Length; i++) {
                var guid = allMonoScripts[i];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                
                EditorUtility.DisplayProgressBar("Extracting Project Information", $"Processing {assetPath}", (float)i / allMonoScripts.Length);
                
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (obj is not MonoScript monoScript) continue;
                
                var objType = monoScript.GetClass();
                if (objType == null) {
                    badGuids++;
                    continue;
                }
                
                yield return new GuidResult(objType.Assembly.FullName, objType.FullName, guid);
            }
            
            EditorUtility.ClearProgressBar();
        }

        private static IEnumerable<AssetGuidResult> GetAssetResults(bool onlyInProjectAssets) {
            var settings = ModuleUtility.GetPatcherSettings();
            // var gamePath = settings.GetLethalCompanyGamePath();
            // string sos;
            // if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
            //     sos = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            // } else {
            //     sos = Path.Combine(settings.GetLethalCompanyGamePath(), "MonoBehaviour");
            // }
            
            var allAssets = !onlyInProjectAssets
                //? AssetDatabase.FindAssets("t:Object", new[] { gamePath })
                ? AssetDatabase.FindAssets("t:Object")
                : AssetDatabase.FindAssets("t:Object", new[] { "Assets" });

            for (var i = 0; i < allAssets.Length; i++) {
                var guid = allAssets[i];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                
                EditorUtility.DisplayProgressBar("Extracting Project Information", $"Processing {assetPath}", (float)i / allAssets.Length);
                
                var fullAssetPath = Path.GetFullPath(assetPath);
                var metaPath = fullAssetPath + ".meta";
                if (!File.Exists(metaPath)) {
                    Debug.LogWarning($"No meta file found for {assetPath}");
                    continue;
                }
                
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (!obj) continue;

                var metaContent = File.ReadAllText(metaPath);
                var match = GuidPatcherModule.GuidPattern.Match(metaContent);
                if (!match.Success) {
                    continue;
                }

                var actualGuid = match.Groups["guid"].Value;
                if (string.IsNullOrEmpty(actualGuid)) {
                    continue;
                }

                // if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long fileId)) {
                //     yield return new AssetGuidResult(assetPath, actualGuid, fileId.ToString());
                // }
                
                yield return new AssetGuidResult(assetPath, actualGuid, null);
            }
            
            EditorUtility.ClearProgressBar();
        }
        
        public static ExtractedResults GetExtractedResults(string filePath) {
            var json = System.IO.File.ReadAllText(filePath);
            return JsonUtility.FromJson<ExtractedResults>(json);
        }
        
        [System.Serializable]
        public class ExtractedResults {
           public GuidResult[] guids = Array.Empty<GuidResult>();
           public AssetGuidResult[] assetGuids = Array.Empty<AssetGuidResult>();
        }

        [System.Serializable]
        public struct GuidResult {
            public string assemblyName;
            public string fullTypeName;
            public string originalGuid;
            
            public GuidResult(string assemblyName, string fullTypeName, string originalGuid) {
                this.assemblyName = assemblyName;
                this.fullTypeName = fullTypeName;
                this.originalGuid = originalGuid;
            }
        }

        [System.Serializable]
        public struct AssetGuidResult {
            public string assetPath;
            public string originalGuid;
            public string fileId;
            
            public AssetGuidResult(string assetPath, string originalGuid, string fileId) {
                this.assetPath = assetPath;
                this.originalGuid = originalGuid;
                this.fileId = fileId;
            }
        }
    }
}
