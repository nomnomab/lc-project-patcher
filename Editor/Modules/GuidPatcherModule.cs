using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace Nomnom.LCProjectPatcher.Modules {
    // ? used as a heavy reference for the monoscripts and shaders after mine was too convoluted
    // ? https://github.com/ChrisFeline/AssetRipperGuidPatcher/blob/main/Assets/Kittenji/Editor/AssetRipperGuidPatch.cs
    using GuidList = System.Collections.Generic.Dictionary<string, GuidPatcherModule.GuidSwap>;
    
    public static class GuidPatcherModule {
        public readonly struct GuidSwap {
            public readonly string name;
            public readonly string guid;
            public readonly string file;
            
            public GuidSwap(string name, string guid, string file) {
                this.name = name;
                this.guid = guid;
                this.file = file;
            }
        }
        
        public readonly static Regex GuidPattern = new(@"guid:\s(?<guid>[0-9A-Za-z]+)", RegexOptions.Compiled);
        public readonly static Regex FullGuidPattern = new(@"{fileID: (?<file>\d+), guid: (?<guid>[0-9A-f-a-f]+), type: (?<type>\d+)}", RegexOptions.Compiled);
        public readonly static Regex ScriptPattern = new(@"  m_Script: {fileID: (?<file>\d+), guid: (?<guid>[0-9A-f-a-f]+), type: (?<type>\d+)}", RegexOptions.Compiled);
        public readonly static Regex ShaderPropPattern = new(@"  m_Shader: {fileID: (?<file>\d+), guid: (?<guid>[0-9A-f-a-f]+), type: (?<type>\d+)}", RegexOptions.Compiled);
        public readonly static Regex ShaderNamePattern = new(@"Shader\s+""(?<name>.*)""[\s\S\r]*?{", RegexOptions.Compiled);
        public readonly static Regex NamespacePattern = new(@"namespace\s+(?<namespace>[\w\.]+)", RegexOptions.Compiled);
        
        private static GuidList _monoList = new();
        private static GuidList _scriptableObjectList = new();
        private static GuidList _shaderList = new();
        private static GuidList _animationClipList = new();

        private readonly static string[] IgnoreScriptFolders = {
            "Unity.Services",
            "Unity.Timeline",
            "Unity.Multiplayer",
            // "Unity.InputSystem",
            "Unity.Burst",
            // "DissonanceVoip",
            // "Facepunch",
            "Unity.Collections",
            "Unity.Jobs",
            "Unity.Networking",
            "Unity.ProBuilder"
        };

        private readonly static string[] ScriptableObjectFolders = {
            "MonoBehaviour",
            "Resources"
        };
        
        private readonly static Type[] AllTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(Component).IsAssignableFrom(x) || typeof(ScriptableObject).IsAssignableFrom(x) || typeof(MonoScript).IsAssignableFrom(x))
            .ToArray();

        public static UniTask Patch(bool debugMode = false) {
            _monoList.Clear();
            _scriptableObjectList.Clear();
            _shaderList.Clear();
            _animationClipList.Clear();
            
            var assetRipperPath = ModuleUtility.GetAssetRipperCloneDirectory();
            
            CheckMonoScripts(assetRipperPath);
            CheckScriptableObjectsScripts(assetRipperPath);
            CheckShaders(assetRipperPath);
            CheckAnimationClips(assetRipperPath);
            
            FixGuids(assetRipperPath, debugMode: debugMode);
            
            return UniTask.CompletedTask;
        }

        private static void CheckMonoScripts(string assetRipperPath) {
            var monoScripts = AssetDatabase.FindAssets("t:MonoScript");

            for (var i = 0; i < monoScripts.Length; i++) {
                var scriptGuid = monoScripts[i];
                EditorUtility.DisplayProgressBar("Checking MonoScript GUIDs", $"Checking {scriptGuid}", (float)i / monoScripts.Length);
                
                var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(scriptGuid));
                // Debug.Log($"Found {mono.name} | {scriptGuid}");
                
                var classType = mono.GetClass();
                if (classType == null) {
                    continue;
                }
                
                if (!classType.IsSubclassOf(typeof(Component))) {
                    continue;
                }

                var assemblyName = classType.Assembly.GetName().Name;
                var namespaceName = string.Empty;
                if (assemblyName == "lethal-company") {
                    assemblyName = "Assembly-CSharp";
                    
                    var baseType = classType.BaseType;
                    if (baseType != null && baseType.Namespace != null) {
                        namespaceName = baseType.Namespace.Replace('.', Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    }
                }
                
                Debug.Log($"{classType.FullName} in {assemblyName} and {namespaceName}");
                var fullName = classType.FullName.Replace('.', Path.DirectorySeparatorChar);
                if (fullName.StartsWith("LethalCompany")) {
                    fullName = fullName[("LethalCompany".Length + 1)..];
                }

                // if (fullName == "PlayerControllerB") {
                //     fullName = Path.Combine("GameNetcodeStuff", fullName);
                // }
                
                var sourceFile = Path.Combine(assetRipperPath, "Assets", "Scripts", assemblyName, $"{namespaceName}{fullName}.cs");
                
                // make sure this doesn't exist in the project already under Assets/Scripts/Assembly-CSharp
                var checkPath = sourceFile.Replace(Path.Combine(assetRipperPath, "Assets"), Application.dataPath);
                Debug.Log($"Checking if {fullName} exists at {checkPath}");

                var fileExistsInProject = File.Exists(checkPath);
                // if (File.Exists(checkPath)) {
                //     Debug.LogWarning($"Found {fullName} | {scriptGuid} already in project");
                //     guid = scriptGuid;
                //     fileId = "11500000";
                // }
                
                // Debug.Log($"Found {fullName} | {scriptGuid} at {sourceFile}");
                var metaFile = $"{sourceFile}.meta";

                long? finalFileID = null;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mono, out var guid2, out long fileID)) {
                    Debug.Log($"Found {fullName} | {scriptGuid} to {guid2} | {fileID}");
                    finalFileID = fileID;
                }

                var fileId = finalFileID.ToString();

                if (!File.Exists(sourceFile)) {
                    Debug.LogWarning($"Could not find source file for {fullName} at {sourceFile}");
                    continue;
                }

                if (!File.Exists(metaFile)) {
                    Debug.LogWarning($"Could not find meta file for {fullName} at {metaFile}");
                    continue;
                }

                var metaContents = File.ReadAllText(metaFile);
                var match = GuidPattern.Match(metaContents);
                if (!match.Success) {
                    continue;
                }

                var guid = match.Groups["guid"].Value;
                if (!fileExistsInProject) {
                    if (string.IsNullOrEmpty(guid) || guid == scriptGuid) {
                        continue;
                    }
                } else {
                    // load guid from project script meta file
                    var projectMetaFile = $"{checkPath}.meta";
                    if (!File.Exists(projectMetaFile)) {
                        Debug.LogWarning($"Could not find project meta file for {fullName} at {projectMetaFile}");
                        continue;
                    }
                    
                    var projectMetaContents = File.ReadAllText(projectMetaFile);
                    var projectMatch = GuidPattern.Match(projectMetaContents);
                    if (!projectMatch.Success) {
                        continue;
                    }
                    
                    scriptGuid = projectMatch.Groups["guid"].Value;
                    fileId = "11500000";
                }

                Debug.Log($"Found {fullName} | {guid} to {scriptGuid}");

                _monoList.Add(guid, new GuidSwap(sourceFile, scriptGuid, fileId));
            }
            
            EditorUtility.ClearProgressBar();
        }

        private static void CheckScriptableObjectsScripts(string assetRipperPath) {
            var allScripts = Directory.GetFiles(Path.Combine(assetRipperPath, "Assets", "Scripts"), "*.cs.meta", SearchOption.AllDirectories)
                //.Concat(Directory.GetFiles(Path.Combine(assetRipperPath, "Assets", "Resources"), "*.cs.meta", SearchOption.AllDirectories))
                .Where(x => !IgnoreScriptFolders.Any(x.Contains))
                .ToArray();

            foreach (var folder in ScriptableObjectFolders) {
                var allProjectFiles = Directory.GetFiles(Path.Combine(assetRipperPath, "Assets", folder), "*.asset", SearchOption.AllDirectories);
                var allMetas = allProjectFiles.Select(x => {
                        var text = File.ReadAllText(x);
                        var match = ScriptPattern.Match(text);
                        if (!match.Success) {
                            return null;
                        }

                        var guid = match.Groups["guid"].Value;
                        if (string.IsNullOrEmpty(guid)) {
                            return null;
                        }

                        Debug.Log($"Found {guid} at {Path.GetFileName(x)} in:\n{text}");

                        return new {
                            guid,
                            file = x
                        };
                    })
                    .Where(x => x != null)
                    .ToArray();

                using var _ = DictionaryPool<string, string>.Get(out var guids);
                for (var i = 0; i < allScripts.Length; i++) {
                    var scriptMetaFile = allScripts[i];
                    EditorUtility.DisplayProgressBar("Checking ScriptableObject GUIDs", $"Checking {scriptMetaFile}", (float)i / allScripts.Length);

                    if (!File.Exists(scriptMetaFile)) {
                        Debug.LogWarning($"Could not find script meta file for {Path.GetFileName(scriptMetaFile)}, yet it was found with GetFiles?");
                        continue;
                    }

                    string text;
                    try {
                        text = File.ReadAllText(scriptMetaFile);
                    } catch (Exception e) {
                        Debug.LogWarning($"Could not read script meta file for {Path.GetFileName(scriptMetaFile)}: {e}");
                        continue;
                    }
                    
                    var match = GuidPattern.Match(text);
                    if (!match.Success) {
                        continue;
                    }

                    var guid = match.Groups["guid"].Value;
                    if (string.IsNullOrEmpty(guid)) {
                        continue;
                    }

                    guids.Add(guid, scriptMetaFile);
                    Debug.Log($"Found {guid} at {Path.GetFileName(scriptMetaFile)}");
                }

                // foreach (var (key, value) in guids) {
                //     Debug.Log($" - {key} at {Path.GetFileName(value)}");
                // }

                for (var i = 0; i < allMetas.Length; i++) {
                    var meta = allMetas[i];
                    EditorUtility.DisplayProgressBar("Checking ScriptableObject GUIDs", $"Checking {meta.guid}", (float)i / allMetas.Length);

                    if (!guids.TryGetValue(meta.guid, out var scriptMetaFile)) {
                        Debug.LogWarning($"Could not find script meta file for {meta.guid} at {meta.file}");
                        continue;
                    }

                    if (_scriptableObjectList.ContainsKey(meta.guid)) {
                        continue;
                    }

                    // Debug.Log($"\t- script is {meta.guid} at {Path.GetFileName(scriptMetaFile)}");

                    var sourceFile = scriptMetaFile[..^".meta".Length];
                    if (!File.Exists(sourceFile)) {
                        Debug.LogWarning($"Could not find source file for {meta.guid} at {sourceFile}");
                        continue;
                    }

                    var match = NamespacePattern.Match(File.ReadAllText(sourceFile));
                    var fullName = Path.GetFileNameWithoutExtension(sourceFile);
                    if (match.Success) {
                        var @namespace = match.Groups["namespace"].Value;
                        fullName = $"{@namespace}.{fullName}";
                    }
                    
                    var realType = AllTypes.FirstOrDefault(x => x.FullName == fullName);
                    if (realType == null) {
                        Debug.LogWarning($"Could not find type for {fullName} at {sourceFile}");
                        continue;
                    }

                    var instance = ScriptableObject.CreateInstance(realType);
                    if (!instance) {
                        Debug.LogWarning($"Could not create instance for {fullName} at {sourceFile}");
                        continue;
                    }

                    var monoScript = MonoScript.FromScriptableObject(instance);
                    if (!monoScript) {
                        Debug.LogWarning($"Could not find mono script for {fullName} at {sourceFile}");
                        GameObject.DestroyImmediate(instance);
                        continue;
                    }

                    var globalId = GlobalObjectId.GetGlobalObjectIdSlow(monoScript);
                    GameObject.DestroyImmediate(instance);

                    if (globalId.ToString().Equals("GlobalObjectId_V1-0-00000000000000000000000000000000-0-0")) {
                        Debug.LogWarning($"Could not find global id for {fullName} at {sourceFile}");
                        continue;
                    }

                    var assetGuid = globalId.assetGUID.ToString();
                    var objectId = globalId.targetObjectId;

                    Debug.Log($"\t- Found {fullName} is {realType} | {meta.guid} to {assetGuid}");
                    _scriptableObjectList.Add(meta.guid, new GuidSwap(fullName, assetGuid, objectId.ToString()));
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private static void CheckShaders(string assetRipperPath) {
            var shaderFilesPath = Path.Combine(assetRipperPath, "Assets", "Shader");
            var shaderFiles = Directory.GetFiles(shaderFilesPath);
            
            for (int i = 0; i < shaderFiles.Length; i++) {
                var file = shaderFiles[i];
                EditorUtility.DisplayProgressBar("Checking Shader GUIDs", $"Checking {Path.GetFileName(file)}", (float)i / shaderFiles.Length);
                
                if (!file.EndsWith(".shader")) {
                    continue;
                }
                
                var content = File.ReadAllText(file);
                var meta = File.ReadAllText($"{file}.meta");
                var match = GuidPattern.Match(meta);
                if (!match.Success) {
                    Debug.LogWarning($"Could not find guid for {file}");
                    continue;
                }
                
                var guid = match.Groups["guid"].Value;
                var shaderNameMatch = ShaderNamePattern.Match(content);
                if (!shaderNameMatch.Success) {
                    Debug.LogWarning($"Could not find shader name for {file}");
                    continue;
                }
                
                var shaderName = shaderNameMatch.Groups["name"].Value;
                var shader = Shader.Find(shaderName);
                if (!shader) {
                    Debug.LogWarning($"Could not find shader {shaderName} for {file}");
                    continue;
                }
                
                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(shader);
                var assetGuid = globalId.assetGUID.ToString();
                var objectId = globalId.targetObjectId;
                
                Debug.Log($"Found {shaderName} | {guid} to {assetGuid} | {objectId}");
                _shaderList.Add(guid, new GuidSwap(shaderName, assetGuid, objectId.ToString()));
            }
            
            EditorUtility.ClearProgressBar();
        }

        private static void CheckAnimationClips(string assetRipperPath) {
            var animationClipFilePath = Path.Combine(assetRipperPath, "Assets", "AnimationClip");
            var animationClipFiles = Directory.GetFiles(animationClipFilePath, "*.anim");

            for (int i = 0; i < animationClipFiles.Length; i++) {
                var file = animationClipFiles[i];
                if (!Path.GetFileName(file).Contains("FaceHalfLit")) continue;
                EditorUtility.DisplayProgressBar("Checking AnimationClip GUIDs", $"Checking {Path.GetFileName(file)}", (float)i / animationClipFiles.Length);
                
                var content = File.ReadAllText(file);
                var matches = FullGuidPattern.Matches(content);
                if (matches.Count == 0) {
                    Debug.LogWarning($"Could not find guid for {Path.GetFileName(file)}");
                    continue;
                }
                
                foreach (Match match in matches) {
                    var guid = match.Groups["guid"].Value;
                    var fileId = match.Groups["file"].Value;
                    // var type = match.Groups["type"].Value;
                    
                    if (string.IsNullOrEmpty(guid)) {
                        Debug.LogWarning($"Could not find guid for {Path.GetFileName(file)} | {guid}");
                        continue;
                    }
                    
                    if (_animationClipList.ContainsKey(guid)) {
                        Debug.LogWarning($"Already found {Path.GetFileName(file)} | {guid}");
                        continue;
                    }

                    if (!_monoList.TryGetValue(guid, out var mono)) {
                        Debug.LogWarning($"Could not find mono for {Path.GetFileName(file)} | {guid}");
                        continue;
                    }
                    
                    Debug.Log($"Found {mono.name} for {Path.GetFileName(file)} | {guid} to {mono.guid}");
                    _animationClipList.Add(guid, mono);
                }
            }
            
            EditorUtility.ClearProgressBar();
        }

        private static void FixGuids(string assetRipperPath, bool debugMode) {
            var files = Directory.GetFiles(assetRipperPath, "*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++) {
                var file = files[i];
                var extension = Path.GetExtension(file).ToLowerInvariant();
                EditorUtility.DisplayProgressBar("Fixing GUIDs", $"Fixing {Path.GetFileName(file)}", (float)i / files.Length);

                switch (extension) {
                    case ".prefab":
                    case ".unity":
                        if (_monoList.Count != 0) {
                            var content = File.ReadAllText(file);
                            var count = 0;
                            content = FullGuidPattern.Replace(content,
                                x => {
                                    var fileID = x.Groups["fileID"].Value;
                                    var guid = x.Groups["guid"].Value;
                                    var type = x.Groups["type"].Value;
                                    if (!_monoList.TryGetValue(guid, out var swap)) {
                                        return x.Value;
                                    }

                                    count++;
                                    fileID = string.IsNullOrEmpty(swap.file) ? fileID : swap.file;
                                    Debug.Log($"Found {swap.name} in {fileID} | {guid} to {swap.guid}");
                                    return $"{{fileID: {fileID}, guid: {swap.guid}, type: {type}}}";
                                });

                            if (count > 0) {
                                Debug.Log($"Fixed {count} guids in {Path.GetFileName(file)}");
                                if (!debugMode) {
                                    File.WriteAllText(file, content);
                                }
                            }
                        }
                        break;
                    case ".asset":
                        if (_scriptableObjectList.Count != 0) {
                            var content = File.ReadAllText(file);
                            var count = 0;
                            content = ScriptPattern.Replace(content,
                                x => {
                                    var guid = x.Groups["guid"].Value;
                                    // var file = x.Groups["file"].Value;
                                    var type = x.Groups["type"].Value;
                                    if (!_scriptableObjectList.TryGetValue(guid, out var swap)) {
                                        return x.Value;
                                    }

                                    count++;
                                    Debug.Log($"Found {swap.name} in {file} | {guid} to {swap.guid}");
                                    return $"  m_Script: {{fileID: {swap.file}, guid: {swap.guid}, type: {type}}}";
                                });

                            if (count > 0) {
                                Debug.Log($"Fixed {count} guids in {Path.GetFileName(file)}");
                                if (!debugMode) {
                                    File.WriteAllText(file, content);
                                }
                            }
                        }
                        break;
                    case ".material":
                    case ".mat":
                        if (_shaderList.Count != 0) {
                            var content = File.ReadAllText(file);
                            var count = 0;
                            content = ShaderPropPattern.Replace(content,
                                x => {
                                    var guid = x.Groups["guid"].Value;
                                    var fileId = x.Groups["file"].Value;
                                    var type = x.Groups["type"].Value;

                                    if (!_shaderList.TryGetValue(guid, out var swap)) {
                                        return x.Value;
                                    }

                                    count++;
                                    Debug.Log($"Found {fileId} in {file} | {guid} to {swap.guid}");
                                    return $"  m_Shader: {{fileID: {swap.file}, guid: {swap.guid}, type: {type}}}";
                                });

                            if (count > 0) {
                                Debug.Log($"Fixed {count} guids in {Path.GetFileName(file)}");
                                if (!debugMode) {
                                    File.WriteAllText(file, content);
                                }
                            }
                        }
                        break;
                    case ".anim":
                        if (_animationClipList.Count != 0) {
                            var content = File.ReadAllText(file);
                            var count = 0;
                            content = FullGuidPattern.Replace(content,
                                x => {
                                    var guid = x.Groups["guid"].Value;
                                    var fileId = x.Groups["file"].Value;
                                    var type = x.Groups["type"].Value;

                                    if (!_animationClipList.TryGetValue(guid, out var swap)) {
                                        return x.Value;
                                    }

                                    count++;
                                    Debug.Log($"Found {fileId} in {file} | {guid} to {swap.guid}");
                                    return $"{{fileID: {swap.file}, guid: {swap.guid}, type: {type}}}";
                                });

                            if (count > 0) {
                                Debug.Log($"Fixed {count} guids in {Path.GetFileName(file)}");
                                if (!debugMode) {
                                    File.WriteAllText(file, content);
                                }
                            }
                        }
                        break;
                }
            }
            
            EditorUtility.ClearProgressBar();
        }
    }
}
