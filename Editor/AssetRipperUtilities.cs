using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher {
    public static class AssetRipperUtilities {
        private static readonly string[] IgnoredStartsWithNamespaces = {
            "UnityEditor",
            "UnityEngine.Rendering.UI",
            "Unity.Services",
            "Unity.Netcode.Transports",
            "Dissonance",
            "Unity.AI",
            "UnityEngine.VFX.Utility",
            "UnityEngine.Timeline",
            "UnityEngine.Rendering.HighDefinition.Compositor",
            "UnityEngine.InputSystem",
            "Unity.Netcode.Samples",
            "DigitalRuby"
        };
        
        private readonly static Dictionary<GuidType, string[]> GUIDToType = new() {
            {GuidType.Component, new[] {"*.prefab", "*.unity"}},
            {GuidType.Asset, new[] {"*.asset", "*.unity"}}
        };
        
        private readonly static string[] PatchFolders = new[] {
            "PrefabInstance",
            "Scenes",
            "MonoBehaviour"
        };

        private static string GetValidPath(string path, string directory) {
            path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            path = Path.Combine(path, "Assets", directory);
            return path;
        }

        public static IEnumerable<(string file, string meta)> LoadFilesWithMetas(string path, string directory, string fileExtension) {
            path = GetValidPath(path, directory);
            var allFiles = Directory.GetFiles(path, fileExtension, SearchOption.AllDirectories)
                .Where(f => {
                    var directory = Path.GetDirectoryName(f);
                    // var directoryName = Path.GetFileNameWithoutExtension(directory);
                    var folders = directory.Split(Path.DirectorySeparatorChar);
                    foreach (var folder in folders) {
                        if (folder == "Editor") {
                            return false;
                        }
                    }
                    return true;
                });
            
            var allFilesWithMetas = allFiles
                .Select(f => (f, $"{f}.meta"))
                .Where(f => File.Exists(f.Item2));

            return allFilesWithMetas;
        }

        public static IEnumerable<(string file, string guid)> LoadFilesWithGuid(IEnumerable<(string file, string meta)> files) {
            foreach (var found in files) {
                var (file, meta) = (found.file, found.meta);
                // var fileText = File.ReadAllText(file);
                var metaFileText = File.ReadAllText(meta);
                var metaGuid = metaFileText.Split('\n')
                    .Where(l => l.StartsWith("guid:"))
                    .Select(l => l.Split(' ')[1])
                    .FirstOrDefault();
                
                yield return (file, metaGuid);
            }
        }

        public static IEnumerable<string> FindFilesWithGuid(string path, string directory, string fileExtension, string guid) {
            path = GetValidPath(path, directory);
            var allFiles = Directory.GetFiles(path, fileExtension, SearchOption.AllDirectories)
                .Where(f => {
                    var contents = File.ReadAllText(f);
                    return contents.Contains(guid);
                });
            
            return allFiles;
        }

        public static IEnumerable<ScriptInfo> LoadScriptInfo(string path, string directory) {
            var allFilesWithMetas = LoadFilesWithMetas(path, directory, "*.cs").ToArray();
            foreach (var (file, guid) in LoadFilesWithGuid(allFilesWithMetas)) {
                if (guid is null) {
                    continue;
                }
                
                var fileText = File.ReadAllText(file);
                var scriptNamespace = fileText.Split('\n')
                    .Where(l => l.StartsWith("namespace "))
                    .Select(l => l.Split(' ')[1])
                    .FirstOrDefault();

                // ignore namespaces meant for the editor
                if (scriptNamespace is not null &&
                    (scriptNamespace.Split('.').Any(n => n == "Editor") || IgnoredStartsWithNamespaces.Any(n => scriptNamespace.StartsWith(n)))) {
                    continue;
                }

                var scriptFileName = Path.GetFileNameWithoutExtension(file);
                yield return new ScriptInfo(string.IsNullOrEmpty(scriptNamespace) ? null : scriptNamespace, scriptFileName, guid);
            }
        }

        public static string GetGuidOfType(Type type, out GuidType guidType) {
            guidType = default;

            if (typeof(ScriptableObject).IsAssignableFrom(type)) {
                var hiddenObject = ScriptableObject.CreateInstance(type);
                try {
                    if (hiddenObject && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(hiddenObject, out var guid, out long _)) {
                        guidType = GuidType.Asset;
                        return guid;
                    }
                    else {
                        var scriptableObject = MonoScript.FromScriptableObject(hiddenObject);
                        var scriptPath = AssetDatabase.GetAssetPath(scriptableObject);
                        var scriptGUID = AssetDatabase.AssetPathToGUID(scriptPath);
                        guidType = GuidType.Asset;
                        return scriptGUID;
                    }
                }
                finally {
                    Object.DestroyImmediate(hiddenObject);
                }
            }
            else if (typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(Component).IsAssignableFrom(type)) {
                var hiddenObject = new GameObject {
                    hideFlags = HideFlags.HideAndDontSave
                };

                try {
                    var component = hiddenObject.AddComponent(type);
                    if (component && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out var guid, out long _)) {
                        guidType = GuidType.Component;
                        return guid;
                    }
                    else {
                        if (typeof(MonoBehaviour).IsAssignableFrom(type)) {
                            var monoScript = MonoScript.FromMonoBehaviour(component as MonoBehaviour);
                            var scriptPath = AssetDatabase.GetAssetPath(monoScript);
                            var scriptGUID = AssetDatabase.AssetPathToGUID(scriptPath);
                            guidType = GuidType.Component;
                            return scriptGUID;
                        }
                        // Debug.LogWarning($"Could not get guid for {type.FullName}");
                        return null;
                    }
                }
                finally {
                    Object.DestroyImmediate(hiddenObject);
                }
            }

            // Debug.LogWarning($"Could not get guid for {type.FullName}");
            return null;
        }

        public static IEnumerable<(string file, string guid)> ScanAssetRipperProjectUnknown(string assetRipperProjectPath, string directory, string fileExtension) {
            var files = LoadFilesWithMetas(assetRipperProjectPath, directory, fileExtension).ToArray();
            return LoadFilesWithGuid(files);
        }

        public static Dictionary<GuidType, (HashSet<string> files, List<(ScriptInfo typeInfo, string guid)>)> ScanAssetRipperProjectScript(string assetRipperProjectPath, string directory, string fileExtension) {
            var rootPath = Application.dataPath;
            
            // group extension to directory files found
            var extensionGroups = new Dictionary<GuidType, HashSet<string>>();
            foreach (var (key, value) in GUIDToType) {
                extensionGroups[key] = new HashSet<string>();
                foreach (var extension in value) {
                    foreach (var folder in PatchFolders) {
                        var path = Path.Combine(rootPath, folder);
                        if (!Directory.Exists(path)) {
                            continue;
                        }

                        var files = Directory.GetFiles(path, extension, SearchOption.AllDirectories);
                        var hashSet = extensionGroups[key];
                        foreach (var file in files) {
                            hashSet.Add(file);
                        }
                    }
                }
            }

            // var allTypes = LoadTypeInfo(Path.Combine(assetRipperProjectPath, "Assets", directory), fileExtension)
            var allTypes = LoadScriptInfo(assetRipperProjectPath, directory)
                .Select(x => (x, x.TryGetType(out var type) ? type : null))
                .Where(x => x.Item2 is not null)
                .Select(x => (x.Item1, GetGuidOfType(x.Item2, out var guidType), guidType))
                .Where(x => !string.IsNullOrEmpty(x.Item2))
                .ToArray();

            Debug.Log($"Found {allTypes.Length} types");

            var combined = new Dictionary<GuidType, (HashSet<string> files, List<(ScriptInfo typeInfo, string guid)>)>();
            foreach (var (typeInfo, newGuid, guidType) in allTypes) {
                if (!combined.TryGetValue(guidType, out var data)) {
                    data = (new HashSet<string>(), new List<(ScriptInfo typeInfo, string guid)>());
                    combined[guidType] = data;
                }

                var files = extensionGroups[guidType];
                foreach (var file in files) {
                    data.files.Add(file);
                }

                data.Item2.Add((typeInfo, newGuid));
            }

            return combined;
        }
        
        public static async UniTask PatchProjectGUIDs(Dictionary<GuidType, (HashSet<string> files, List<(ScriptInfo typeInfo, string guid)>)> combined, Action callback) {
            await UniTask.SwitchToTaskPool();

            var totalChecks = combined.Values.Sum(x => x.files.Count);
            var totalIndex = 0;
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            foreach (var (key, data) in combined) {
                Debug.Log($"Patching {data.files.Count} {key} files with {data.Item2.Count} type checks per file");

                var result = Parallel.ForEach(data.files, file => {
                    var sb = new StringBuilder(File.ReadAllText(file));
                    foreach (var (typeInfo, newGuid) in data.Item2) {
                        sb.Replace(typeInfo.guid, newGuid);
                    }

                    File.WriteAllText(file, sb.ToString());

                    totalIndex++;
                    Debug.Log($"- [{totalIndex}/{totalChecks}] {Path.GetFileName(file)} patched");
                });

                await UniTask.WaitUntil(() => result.IsCompleted);
            }

            Debug.Log($"Finished patching in {stopwatch.ElapsedMilliseconds}ms");

            await UniTask.SwitchToMainThread();
            callback();
            AssetDatabase.Refresh();
        }

        public static string GetShaderPathFromShader(string fileContents) {
            var shaderPath = Regex.Match(fileContents, @"^Shader ""(.*?)""", RegexOptions.Multiline).Groups[1].Value;
            return shaderPath;
        }
    }
}
