using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lachee.Utilities.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class InputActionsModule {
        public static void FixAll(LCPatcherSettings settings) {
            string soPath;
            if (settings.AssetRipperSettings.TryGetMapping("MonoBehaviour", out var finalFolder)) {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                soPath = Path.Combine(settings.GetLethalCompanyGamePath(), "MonoBehaviour");
            }

            var inputActionsPath = Path.Combine(soPath, "UnityEngine", "InputActionAsset");
            var allInputActions = AssetDatabase.FindAssets("t:InputActionAsset",
                new string[] {
                    inputActionsPath
                });
            
            foreach (var inputAction in allInputActions) {
                var assetPath = AssetDatabase.GUIDToAssetPath(inputAction);
                var originalGuid = AssetDatabase.AssetPathToGUID(assetPath);
                // ? need this otherwise unity won't load up all the input data
                var inputActionAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, inputActionAssetType);
                var clone = Object.Instantiate(asset);
                var realPath = Path.GetFullPath(assetPath);
                var text = File.ReadAllText(realPath);
                if (!text.Trim().StartsWith("%YAML")) continue;

                Debug.Log($"Fixing InputActionAsset ({asset}) at \"{assetPath}\"");

                clone.name = $"{Path.GetFileNameWithoutExtension(realPath)}";

                // some terrible string manipulation to fix the json, but idc it works
                var json = JsonUtility.ToJson(clone, true);
                var lines = json.Split('\n').ToList();
                lines.Insert(1, $"  \"m_Name\": \"{clone.name}\",");
                json = string.Join("\n", lines);
                foreach (var group in Regex.Matches(json, @"""(m_[^""]*)""").ToArray()) {
                    var value = group.Value.Replace("m_", string.Empty);
                    var charArray = value.ToCharArray();
                    charArray[1] = char.ToLower(charArray[1]);
                    value = new string(charArray);
                    json = json.Replace(group.Value, value);
                }
                json = json.Replace("\"actionMaps\"", "\"maps\"");

                var jsonObj = JObject.Parse(json);
                var maps = jsonObj["maps"];

                foreach (JObject map in maps) {
                    var actions = map["actions"];
                    var assetProperty = map.Property("asset");
                    assetProperty?.Remove();
                    
                    foreach (JObject action in actions) {
                        var newAction = new JObject();
                        var flags = action["flags"].Value<int>();
                        action["initialStateCheck"] = flags == 1;
                        
                        var type = action["type"].Value<int>();
                        action["type"] = type switch {
                            0 => "Value",
                            1 => "Button",
                            2 => "PassThrough",
                            _ => throw new ArgumentOutOfRangeException()
                        };
                        
                        var flagProperty = action.Property("flags");
                        flagProperty?.Remove();
                        
                        var singletonActionBindingsProperty = action.Property("singletonActionBindings");
                        singletonActionBindingsProperty?.Remove();
                    }

                    var bindings = map["bindings"];
                    foreach (JObject binding in bindings) {
                        var flags = binding["flags"].Value<int>();
                        binding["isComposite"] = (flags & 4) == 4;
                        binding["isPartOfComposite"] = (flags & 8) == 8;
                        
                        var flagProperty = binding.Property("flags");
                        flagProperty?.Remove();
                    }
                }
                
                var controlSchemes = jsonObj["controlSchemes"];
                foreach (JObject controlScheme in controlSchemes) {
                    var devices = new JArray();
                    var deviceRequirements = controlScheme["deviceRequirements"];
                    foreach (JObject deviceRequirement in deviceRequirements) {
                        deviceRequirement["devicePath"] = deviceRequirement["controlPath"];
                        
                        var flags = deviceRequirement["flags"].Value<int>();
                        deviceRequirement["isOptional"] = (flags & 1) == 1;
                        deviceRequirement["isOR"] = (flags & 2) == 2;
                        
                        var flagProperty = deviceRequirement.Property("flags");
                        flagProperty?.Remove();
                        
                        var controlPathProperty = deviceRequirement.Property("controlPath");
                        controlPathProperty?.Remove();
                        
                        devices.Add(deviceRequirement);
                    }
                    
                    var deviceRequirementsProperty = controlScheme.Property("deviceRequirements");
                    deviceRequirementsProperty?.Remove();
                    
                    controlScheme["devices"] = devices;
                }
                
                json = jsonObj.ToString(Formatting.Indented);
                
                var newPath = Path.Combine(Path.GetDirectoryName(realPath), $"{Path.GetFileNameWithoutExtension(realPath)}.inputactions");
                File.WriteAllText(newPath, json);
                
                AssetDatabase.Refresh();

                var localNewPath = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), newPath);
                var newGuid = AssetDatabase.AssetPathToGUID(localNewPath);
                var newObj = AssetDatabase.LoadAssetAtPath(localNewPath, inputActionAssetType);
                
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newObj, out var guid, out long fileId)) {
                    Debug.Log($"GUID: {guid}, FileID: {fileId}");
                    GuidPatcherModule.FixGuidsForScriptableObject(originalGuid, guid, fileId.ToString(), 3, inputActionAssetType.FullName);
                }
            }
        }
    }
}
