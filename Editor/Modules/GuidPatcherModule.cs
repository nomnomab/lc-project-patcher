using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nomnom.LCProjectPatcher.Editor;
using Nomnom.LCProjectPatcher.Editor.Modules;
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
            public readonly int? type;
            
            public GuidSwap(string name, string guid, string file) {
                this.name = name;
                this.guid = guid;
                this.file = file;
                type = null;
            }
            
            public GuidSwap(string name, string guid, string file, int type) {
                this.name = name;
                this.guid = guid;
                this.file = file;
                this.type = type;
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
            // "Resources"
        };
        
        private readonly static Type[] AllTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(Component).IsAssignableFrom(x) || typeof(ScriptableObject).IsAssignableFrom(x) || typeof(MonoScript).IsAssignableFrom(x))
            .ToArray();

        public static void PatchAll(LCPatcherSettings settings, bool debugMode = false) {
            _monoList.Clear();
            _scriptableObjectList.Clear();
            _shaderList.Clear();
            _animationClipList.Clear();

            var assetRipperPath = ModuleUtility.AssetRipperTempDirectoryExportedProject;
            CheckMonoScripts(settings, assetRipperPath);
            CheckScriptableObjectsScripts(assetRipperPath);
            CheckShaders(assetRipperPath);
            CheckAnimationClips(assetRipperPath);
            FixGuids(assetRipperPath, debugMode: debugMode);
        }

        private static void CheckMonoScripts(LCPatcherSettings settings, string assetRipperPath) {
            var monoScripts = AssetDatabase.FindAssets("t:MonoScript");
            var allMetaGuids = GetAllProjectMetaData(settings);
            
            foreach (var (file, guid) in allMetaGuids) {
                Debug.Log($" - {guid.foundNamespace}.{guid.guid} at {file}");
            }

            for (var i = 0; i < monoScripts.Length; i++) {
                var assetGuid = monoScripts[i];
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                EditorUtility.DisplayProgressBar("Checking MonoScript GUIDs", $"Checking {assetGuid}", (float)i / monoScripts.Length);
                
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (obj is not MonoScript mono) {
                    Debug.LogWarning($"Could not load mono script for {obj} at {assetGuid}:{assetPath}");
                    continue;
                }
                
                var classType = mono.GetClass();
                string sourceFile;
                string goodGuid;
                long fileID;
                if (classType != null && classType.IsSubclassOf(typeof(Component))) {
                    var assemblyName = classType.Assembly.GetName().Name;
                    Debug.Log($"{classType.FullName} in {assemblyName}");
                
                    var fullName = classType.FullName.Replace('.', Path.DirectorySeparatorChar);
                    sourceFile = Path.Combine(assetRipperPath, "Assets", "Scripts", assemblyName, $"{fullName}.cs");
                    Debug.Log($"Checking if {fullName} exists in the project");
                
                    long? finalFileID = null;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mono, out goodGuid, out fileID)) {
                        Debug.Log($"Found {fullName} | {assetGuid} to {goodGuid} | {fileID}");
                        finalFileID = fileID;
                    }

                    var fileId = finalFileID.ToString();
                    if (!File.Exists(sourceFile) || !File.Exists($"{sourceFile}.meta")) {
                        Debug.LogWarning($"Could not find source/meta file for {fullName} at {sourceFile}");
                        continue;
                    }
                } else if (allMetaGuids.TryGetValue($"{assetPath.Replace('/', Path.DirectorySeparatorChar)}.meta", out var guid)) {
                    Debug.Log($"Found guid: \"{guid.foundNamespace}\".{guid.guid}");
                    var fullName = guid.foundNamespace.Replace('.', Path.DirectorySeparatorChar);
                    var type = AllTypes.FirstOrDefault(x => x.FullName == $"{guid.foundNamespace}.{mono.name}");
                    if (type == null) {
                        Debug.LogWarning($"Could not find type for {mono.name}");
                        continue;
                    }
                    
                    sourceFile = Path.Combine(assetRipperPath, "Assets", "Scripts", type.Assembly.GetName().Name, $"{type.FullName.Replace('.', Path.DirectorySeparatorChar)}.cs");
                    goodGuid = guid.guid;
                    fileID = 11500000;
                } else {
                    Debug.LogWarning($"Could not find guid for {mono.name} at {assetPath}");
                    continue;
                }
                
                // get the bad guid from the ripper directory
                var metaFile = $"{sourceFile}.meta";
                string metaContents;
                try {
                    metaContents = File.ReadAllText(metaFile);
                } catch (Exception e) {
                    Debug.LogWarning($"Could not read meta file for at {metaFile}: {e}");
                    continue;
                }
                
                var match = GuidPattern.Match(metaContents);
                if (!match.Success) {
                    Debug.LogWarning($"- Could not find guid for {metaFile}");
                    continue;
                }
                    
                var badGuid = match.Groups["guid"].Value;
                Debug.Log($"Found {mono.name} | {badGuid} to {goodGuid} | {sourceFile}");
                _monoList.TryAdd(badGuid, new GuidSwap(sourceFile, goodGuid, fileID.ToString()));
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
                                    
                                    var str = $"{{fileID: {fileID}, guid: {swap.guid}, type: {(swap.type?.ToString() ?? type)}}}";
                                    Debug.Log($"Found {swap.name} in {fileID} | {str}");
                                    return str;
                                });

                            if (count > 0) {
                                Debug.Log($"Fixed {count} guids in {Path.GetFileName(file)}");
                                if (!debugMode) {
                                    File.WriteAllText(file, content);
                                }
                            }
                        }

                        // todo: make sure this works fine
                        if (_scriptableObjectList.Count != 0) {
                            var content = File.ReadAllText(file);
                            var count = 0;
                            content = FullGuidPattern.Replace(content,
                                x => {
                                    var fileID = x.Groups["fileID"].Value;
                                    var guid = x.Groups["guid"].Value;
                                    var type = x.Groups["type"].Value;
                                    if (!_scriptableObjectList.TryGetValue(guid, out var swap)) {
                                        return x.Value;
                                    }

                                    count++;
                                    fileID = string.IsNullOrEmpty(swap.file) ? fileID : swap.file;
                                    
                                    var str = $"{{fileID: {fileID}, guid: {swap.guid}, type: {(swap.type?.ToString() ?? type)}}}";
                                    Debug.Log($"Found {swap.name} in {fileID} | {str}");
                                    return str;
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
                                    
                                    var str = $"  m_Script: {{fileID: {swap.file}, guid: {swap.guid}, type: {(swap.type?.ToString() ?? type)}}}";
                                    Debug.Log($"Found {swap.name} in {file} | {str}");
                                    return str;
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
                                    
                                    var str = $"  m_Shader: {{fileID: {swap.file}, guid: {swap.guid}, type: {(swap.type?.ToString() ?? type)}}}";
                                    Debug.Log($"Found {fileId} in {file} | {str}");
                                    return str;
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
                                    
                                    var str = $"{{fileID: {swap.file}, guid: {swap.guid}, type: {(swap.type?.ToString() ?? type)}}}";
                                    Debug.Log($"Found {fileId} in {file} | {str}");
                                    return str;
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
        
        public static void CreateES3DefaultsScript(LCPatcherSettings settings) {
            // ? this one is in Resources so it doesn't get picked up automatically for some reason
            var es3DefaultsFormat = Resources.Load<TextAsset>("WrapperScriptTemplate").text;
            
            // var es3DefaultsPath = ModuleUtility.GetProjectDirectory("Scripts", "Resources", "es3");
            string scripts;
            if (settings.AssetRipperSettings.TryGetMapping("Scripts", out var finalFolder)) {
                scripts = Path.Combine(settings.GetLethalCompanyGamePath(fullPath: true), finalFolder);
            } else {
                scripts = Path.Combine(settings.GetLethalCompanyGamePath(fullPath: true), "Scripts");
            }
            
            var es3DefaultsPath = Path.Combine(scripts, "es3");
            Directory.CreateDirectory(es3DefaultsPath);
            
            es3DefaultsFormat = es3DefaultsFormat
                .Replace("$CLASS_NAME$", "ES3Defaults")
                .Replace("$BASE_CLASS$", "global::ES3Defaults");
            
            File.WriteAllText(Path.Combine(es3DefaultsPath, "ES3Defaults.cs"), es3DefaultsFormat);
        }

        public static void FixGuidsWithPatcherList(ExtractProjectInformationUtility.ExtractedResults extractedResults) {
            _monoList.Clear();
            _scriptableObjectList.Clear();
            _shaderList.Clear();
            _animationClipList.Clear();
            
            // var settings = ModuleUtility.GetPatcherSettings();
            // var allMetaGuids = GetAllProjectMetaData(settings);
            var thisProjectExtractedResults = ExtractProjectInformationUtility.CreateExtractedResults();
            
            foreach (var originalResult in extractedResults.guids) {
                foreach (var projectResult in thisProjectExtractedResults.guids) {
                    if (originalResult.fullTypeName != projectResult.fullTypeName)continue;
                    if (originalResult.originalGuid == projectResult.originalGuid) continue;
                    
                    // Debug.Log($"Found {originalResult.fullTypeName}::{originalResult.originalGuid} to {projectResult.fullTypeName}::{projectResult.originalGuid}");
                    _monoList.TryAdd(originalResult.originalGuid, new GuidSwap(projectResult.fullTypeName, projectResult.originalGuid, "11500000"));
                    _scriptableObjectList.TryAdd(originalResult.originalGuid, new GuidSwap(projectResult.fullTypeName, projectResult.originalGuid, "11500000"));
                }
            }
            
            var assetRipperPath = ModuleUtility.ProjectDirectory;
            FixGuids(assetRipperPath, debugMode: false);
            
            _monoList.Clear();
            _scriptableObjectList.Clear();
            
            AssetDatabase.Refresh();
        }
        
        public static void FixGuidsForScriptableObject(string originalGuid, string newGuid, string fileId, int type, string fullTypeName) {
            _monoList.Clear();
            _shaderList.Clear();
            _animationClipList.Clear();
            _scriptableObjectList.Clear();
            _scriptableObjectList.TryAdd(originalGuid, new GuidSwap(fullTypeName, newGuid, fileId, type));
            var assetRipperPath = ModuleUtility.ProjectDirectory;
            FixGuids(assetRipperPath, debugMode: false);
            _scriptableObjectList.Clear();
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, (string guid, string foundNamespace)> GetAllProjectMetaData(LCPatcherSettings settings) {
            var projectRoot = settings.GetBasePath(fullPath: true);
            var allProjectMetaFiles = Directory.GetFiles(projectRoot, "*.cs.meta", SearchOption.AllDirectories);
            return allProjectMetaFiles.Select(x => {
                    try {
                        var text = File.ReadAllText(x);
                        var match = GuidPattern.Match(text);
                        if (!match.Success) {
                            return null;
                        }

                        var guid = match.Groups["guid"].Value;
                        if (string.IsNullOrEmpty(guid)) {
                            return null;
                        }

                        var relativePath = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), x);
                        // Debug.Log($"Found {guid} at {relativePath} in:\n{text}");
                        
                        var fileContents = File.ReadAllText(x[..^5]);
                        var foundNamespaceMatch = NamespacePattern.Match(fileContents);
                        var foundNamespace = string.Empty;
                        if (foundNamespaceMatch.Success) {
                            var @namespace = foundNamespaceMatch.Groups["namespace"].Value;
                            foundNamespace = @namespace;
                        }

                        return new {
                            guid,
                            file = relativePath,
                            foundNamespace = foundNamespace
                        };
                    } catch (Exception e) {
                        Debug.LogWarning($"Could not read meta file for {x}: {e}");
                        return null;
                    }
                })
                .Where(x => x != null)
                .ToDictionary(x => x.file, x => (x.guid, x.foundNamespace));
        }
    }
}
