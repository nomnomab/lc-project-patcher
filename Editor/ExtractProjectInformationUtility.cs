using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor {
    public static class ExtractProjectInformationUtility {
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Extract Project Information")]
        public static void ExtractProjectInformation() {
            var outputFilePath = EditorUtility.SaveFilePanel("Save Project Information", "", "ProjectInformation", "json");
            if (string.IsNullOrEmpty(outputFilePath)) {
                return;
            }

            var results = CreateExtractedResults();
            var json = JsonUtility.ToJson(results, true);
            System.IO.File.WriteAllText(outputFilePath, json);
            
            Debug.Log($"Extracted {results.guids.Length} guids to {outputFilePath}");
        }

        public static ExtractedResults CreateExtractedResults() {
            var allMonoScripts = AssetDatabase.FindAssets("t:MonoScript");
            var results = new ExtractedResults();

            var goodGuids = new List<GuidResult>();
            var badGuids = 0;
            for (var i = 0; i < allMonoScripts.Length; i++) {
                var guid = allMonoScripts[i];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (obj is not MonoScript monoScript) continue;
                
                var objType = monoScript.GetClass();
                if (objType == null) {
                    badGuids++;
                    continue;
                }
                
                var result = new GuidResult(objType.Assembly.FullName, objType.FullName, guid);
                goodGuids.Add(result);
            }
            
            results.guids = goodGuids.ToArray();
            return results;
        }
        
        public static ExtractedResults GetExtractedResults(string filePath) {
            var json = System.IO.File.ReadAllText(filePath);
            return JsonUtility.FromJson<ExtractedResults>(json);
        }
        
        [System.Serializable]
        public struct ExtractedResults {
           public GuidResult[] guids;
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
    }
}
