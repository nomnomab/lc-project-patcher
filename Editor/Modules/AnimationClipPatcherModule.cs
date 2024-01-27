using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Lachee.Utilities.Serialization;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Modules {
    public static class AnimationClipPatcherModule {
        public readonly struct GuidElement {
            public readonly string fileId;
            public readonly string guid;
            public readonly string type;
            
            public GuidElement(string fileId, string guid, string type) {
                this.fileId = fileId;
                this.guid = guid;
                this.type = type;
            }

            public override string ToString() {
                return $"{{fileID: {fileId}, guid: {guid}, type: {type}}}";
            }
        }
        
        private readonly static Regex PptrCurveMappingItemPattern = new(@"{fileID: (?<file>\d+), guid: (?<guid>[0-9A-f-a-f]+), type: (?<type>\d+)}", RegexOptions.Compiled);
        
        public static UniTask Patch() {
            var assetRipperPath = EditorPrefs.GetString("nomnom.lc_project_patcher.asset_ripper_path");
            var animationClipPath = Path.Combine(assetRipperPath, "Assets", "AnimationClip");
            var animationClipFiles = Directory.GetFiles(animationClipPath, "*.anim", SearchOption.AllDirectories);
            foreach (var animationClipFile in animationClipFiles) {
                var fileName = Path.GetFileName(animationClipFile);
                if (!fileName.Contains("FaceHalfLit")) continue;
                
                var fileContents = File.ReadAllText(animationClipFile);
                var yaml = UYAMLParser.Parse(fileContents);
                
                var isDirty = false;
                
                // todo: convert m_FloatCurves to m_PPtrCurves if isPPtrCurve is true for a given clip
                isDirty |= PatchAssetFloatCurves(yaml[0]);

                if (!isDirty) continue;
                
                // yippie
                var writer = new UYAMLWriter();
                writer.AddComponent(yaml[0]);
                    
                var newFileContents = writer.ToString();
                Debug.Log($"Writing new file contents to {animationClipFile}\n{newFileContents}");
                File.WriteAllText(animationClipFile, newFileContents);
            }
            
            return UniTask.CompletedTask;
        }

        private static bool PatchAssetFloatCurves(UComponent node) {
            // DebugYaml(node.Component);

            if (!TryGetProperty(node.Component, "m_ClipBindingConstant", out UObject clipBindingConstantObject)) {
                Debug.LogWarning($"- No clip binding constant found");
                return false;
            }

            var clipBindingConstants = GetClipBindingConstants(clipBindingConstantObject);
            if (!TryGetProperty(node.Component, "m_FloatCurves", out UArray floatCurvesArray)) {
                Debug.LogWarning($"- No float curves found");
                return false;
            }
            
            if (!TryGetProperty(node.Component, "m_PPtrCurves", out UArray pptrCurvesArray)) {
                return false;
            }

            var floatCurves = GetFloatCurves(floatCurvesArray);
            node.Component.properties["m_FloatCurves"] = new UProperty {
                name = "m_FloatCurves",
                value = new UArray()
            };

            var pptrCurves = ConvertFloatCurvesToPPtrCurves(floatCurves, clipBindingConstants);
            node.Component.properties["m_PPtrCurves"] = new UProperty {
                name = "m_PPtrCurves",
                value = new UArray {
                    items = new List<UNode>(pptrCurves)
                }
            };

            node.Component.properties["m_EditorCurves"] = new UProperty {
                name = "m_EditorCurves",
                value = new UArray()
            };
            
            return true;
        }

        private static IEnumerable<UObject> ConvertFloatCurvesToPPtrCurves(IEnumerable<FloatCurve> floatCurves, ClipBindingConstants clipBindingConstants) {
            var pptrCurves = floatCurves.Select(x => new PPtrCurve(x, clipBindingConstants));
            return pptrCurves.Select(x => new UObject {
                properties = getProperties(x).ToDictionary(y => y.name, y => y)
            });

            IEnumerable<UProperty> getProperties(PPtrCurve curve) {
                yield return getProperty("serializedVersion", new UValue {
                    value = curve.serializedVersion
                });

                yield return getProperty("curve", new UArray {
                    items = new List<UNode>(
                        curve.keyframes.Select(y => new UObject {
                            properties = new() {
                                {"time", new UProperty {
                                    name = "time",
                                    value = new UValue {
                                        value = y.time.ToString(CultureInfo.InvariantCulture)
                                    }
                                }},
                                {"value", new UProperty {
                                    name = "value",
                                    value = new UValue {
                                        value = y.value
                                    }
                                }}
                            }
                        })
                    )
                });
                
                yield return getProperty("attribute", new UValue {
                    value = curve.attribute
                });
                
                yield return getProperty("path", new UValue {
                    value = curve.path
                });
                
                yield return getProperty("classID", new UValue {
                    value = curve.classId
                });
                
                yield return getProperty("script", new UValue {
                    value = curve.script
                });
                
                // ? what is this
                // yield return getProperty("flags", new UValue {
                //     value = curve.flags
                // });
            }

            UProperty getProperty(string name, UNode value) {
                return new UProperty {
                    name = name,
                    value = value
                };
            }
        }

        private static ClipBindingConstants GetClipBindingConstants(UObject obj) {
            var genericBindings = GetGenericBindings(obj).ToArray();
            if (!TryGetProperty(obj, "pptrCurveMapping", out UArray pptrCurveMappingArray)) {
                throw new Exception($"- No pptr curve mapping found");
            }
            
            var mappings = GetObjects(pptrCurveMappingArray.items)
                .Select(x => GetScriptInfo(x))
                .Where(x => x != null)
                .Select(x => x.Value)
                .ToArray();
            
            return new ClipBindingConstants(genericBindings, mappings.Select(x => new GenericBinding.PPtrCurveMapping(x.fileId, x.guid, x.type)).ToArray());
        }

        private static GuidElement? GetScriptInfo(UObject obj) {
            string fileId;
            string guid;
            string type;
            if (TryGetProperty(obj, "{fileID", out UValue data)) {
                var str = $"{{fileID: {data.value}";
                Debug.Log(str);
                var match = PptrCurveMappingItemPattern.Match(str);
                if (!match.Success) {
                    Debug.LogWarning($"- No match found");
                    return null;
                }

                fileId = match.Groups["file"].Value;
                guid = match.Groups["guid"].Value;
                type = match.Groups["type"].Value;
            } else if (TryGetProperty(obj, "fileID", out data) && TryGetProperty(obj, "guid", out UValue guidData) && TryGetProperty(obj, "type", out UValue typeData)) {
                fileId = data.value;
                guid = guidData.value;
                type = typeData.value;
            } else {
                Debug.LogWarning($"- No fileID found");
                return null;
            }

            return new GuidElement(fileId, guid, type);
        }

        private static IEnumerable<GenericBinding> GetGenericBindings(UObject obj) {
            if (!TryGetProperty(obj, "genericBindings", out UArray genericBindings)) {
                yield break;
            }
                    
            foreach (var genericBinding in genericBindings.items.Select(x => x as UObject)) {
                if (genericBinding == null) continue;
                if (!TryGetProperty(genericBinding, "isPPtrCurve", out UNode isPPtrCurve) || !int.TryParse(isPPtrCurve.ToString(), out var isPPtrCurveValue)) {
                    continue;
                }
                
                if (!TryGetProperty(genericBinding, "isIntCurve", out UNode isIntCurve) || !int.TryParse(isIntCurve.ToString(), out var isIntCurveValue)) {
                    continue;
                }
                
                if (!TryGetProperty(genericBinding, "isSerializeReferenceCurve", out UNode isSerializeReferenceCurve) || !int.TryParse(isSerializeReferenceCurve.ToString(), out var isSerializeReferenceCurveValue)) {
                    continue;
                }
                
                yield return new GenericBinding(isPPtrCurveValue, isIntCurveValue, isSerializeReferenceCurveValue);
            }
        }

        private static IEnumerable<FloatCurve> GetFloatCurves(UArray array) {
            var items = array.items;
            var curveHolder = items[0] as UObject;
            if (!TryGetProperty(curveHolder, "serializedVersion", out UValue serializedVersion)) {
                yield break;
            }
            
            if (!TryGetProperty(curveHolder, "curve", out UObject curve)) {
                yield break;
            }
                
            if (!TryGetProperty(curve, "m_Curve", out UArray innerCurve)) {
                yield break;
            }

            var keyframes = GetObjects(innerCurve.items).Select(x => {
                    if (!TryGetProperty(x, "time", out UNode time) || !float.TryParse(time.ToString(), out var timeValue)) {
                        return null as FloatCurveKeyframe?;
                    }

                    if (!TryGetProperty(x, "value", out UNode value) || !int.TryParse(value.ToString(), out var valueValue)) {
                        return null;
                    }

                    return new FloatCurveKeyframe(timeValue, valueValue);
                })
                .Where(x => x != null)
                .Select(x => x.Value);
            
            var attribute = items[1] as UValue;
            var path = items[2] as UValue;
            var classId = items[3] as UValue;
            var scriptInfo = GetScriptInfo(items[4] as UObject);
            if (scriptInfo == null) {
                yield break;
            }

            var script = scriptInfo.ToString();
            var flags = items[5] as UValue;

            yield return new FloatCurve(
                serializedVersion.value ?? string.Empty,
                keyframes.ToArray(),
                attribute?.ToString() ?? string.Empty,
                path?.ToString() ?? string.Empty,
                classId?.ToString() ?? string.Empty,
                script,
                flags?.ToString() ?? string.Empty
            );
        }

        private static IEnumerable<PPtrCurve> GetPPtrCurves(UArray array) {
            var items = array.items;
            if (items.Count == 0) {
                yield break;
            }
            
            var curveHolder = items[0] as UObject;
            if (!TryGetProperty(curveHolder, "serializedVersion", out UValue serializedVersion)) {
                yield break;
            }
            
            if (!TryGetProperty(curveHolder, "curve", out UArray curveArray)) {
                yield break;
            }

            var curve = GetObjects(curveArray.items).Select(x => {
                    if (!TryGetProperty(x, "time", out UNode time) || !float.TryParse(time.ToString(), out var timeValue)) {
                        return null as PPtrCurveKeyframe?;
                    }

                    if (!TryGetProperty(x, "value", out UNode value)) {
                        return null;
                    }

                    return new PPtrCurveKeyframe(timeValue, value.ToString());
                })
                .Where(x => x != null)
                .Select(x => x.Value);
            
            var attribute = items[1] as UValue;
            var path = items[2] as UValue;
            var classId = items[3] as UValue;
            var scriptInfo = GetScriptInfo(items[4] as UObject);
            if (scriptInfo == null) {
                yield break;
            }
            
            var script = scriptInfo.ToString();
            var flags = items[5] as UValue;
            
            yield return new PPtrCurve(
                serializedVersion.value ?? string.Empty,
                curve.ToArray(),
                attribute?.ToString() ?? string.Empty,
                path?.ToString() ?? string.Empty,
                classId?.ToString() ?? string.Empty,
                script,
                flags?.ToString() ?? string.Empty
            );
        }

        private static void DebugYaml(UNode node, string label = null, int depth = 0) {
            var depthPrefix = $"{new string(' ', depth)}{depth}->";
            Debug.Log($"{depthPrefix}[{label}]::{node} ({node.GetType().Name})");
            
            if (node is UObject obj) {
                foreach (var (_, value) in obj.properties) {
                    DebugYaml(value.value, $"prop->{value.name}", depth + 1);
                }
                return;
            }

            if (node is UArray array) {
                Debug.Log($"{depthPrefix}[{label}]::{array}");
                for (var i = 0; i < array.items.Count; i++) {
                    var item = array.items[i];
                    DebugYaml(item, $"index->{i}", depth + 1);
                }
                return;
            }
        }

        private static IEnumerable<UObject> GetObjects(IEnumerable<UNode> nodes) {
            foreach (var node in nodes) {
                yield return node as UObject;
            }
        }
        
        private static bool TryGetProperty<T>(UObject obj, string key, out T value) where T: UNode {
            if (!obj.properties.TryGetValue(key, out var property) || property.value is not T t) {
                value = default;
                return false;
            }

            value = t;
            return true;
        }

        private readonly struct ClipBindingConstants {
            public readonly GenericBinding[] genericBindings;
            public readonly GenericBinding.PPtrCurveMapping[] pptrCurveMappings;
            
            public ClipBindingConstants(GenericBinding[] genericBindings, GenericBinding.PPtrCurveMapping[] pptrCurveMappings) {
                this.genericBindings = genericBindings;
                this.pptrCurveMappings = pptrCurveMappings;
            }
        }

        private readonly struct GenericBinding {
            public readonly bool isPPtrCurve;
            public readonly bool isIntCurve;
            public readonly bool isSerializeReferenceCurve;
            
            public GenericBinding(int isPPtrCurve, int isIntCurve, int isSerializeReferenceCurve) {
                this.isPPtrCurve = isPPtrCurve == 1;
                this.isIntCurve = isIntCurve == 1;
                this.isSerializeReferenceCurve = isSerializeReferenceCurve == 1;
            }

            public readonly struct PPtrCurveMapping {
                public readonly string fileId;
                public readonly string guid;
                public readonly string type;
                
                public PPtrCurveMapping(string fileId, string guid, string type) {
                    this.fileId = fileId;
                    this.guid = guid;
                    this.type = type;
                }

                public override string ToString() {
                    return $"{{fileID: {fileId}, guid: {guid}, type: {type}}}";
                }
            }
        }

        private readonly struct FloatCurve {
            public readonly string serializedVersion;
            public readonly FloatCurveKeyframe[] keyframes;
            public readonly string attribute;
            public readonly string path;
            public readonly string classId;
            public readonly string script;
            public readonly string flags;
            
            public FloatCurve(string serializedVersion, FloatCurveKeyframe[] keyframes, string attribute, string path, string classId, string script, string flags) {
                this.serializedVersion = serializedVersion;
                this.keyframes = keyframes;
                this.attribute = attribute;
                this.path = path;
                this.classId = classId;
                this.script = script;
                this.flags = flags;
            }
        }

        private readonly struct PPtrCurve {
            public readonly string serializedVersion;
            public readonly PPtrCurveKeyframe[] keyframes;
            public readonly string attribute;
            public readonly string path;
            public readonly string classId;
            public readonly string script;
            public readonly string flags;
            
            public PPtrCurve(string serializedVersion, PPtrCurveKeyframe[] keyframes, string attribute, string path, string classId, string script, string flags) {
                this.serializedVersion = serializedVersion;
                this.keyframes = keyframes;
                this.attribute = attribute;
                this.path = path;
                this.classId = classId;
                this.script = script;
                this.flags = flags;
            }

            public PPtrCurve(FloatCurve floatCurve, ClipBindingConstants constants) {
                serializedVersion = floatCurve.serializedVersion;
                keyframes = floatCurve.keyframes.Select(x => new PPtrCurveKeyframe(x.time, constants.pptrCurveMappings[x.value].ToString())).ToArray();
                attribute = floatCurve.attribute;
                path = floatCurve.path;
                classId = floatCurve.classId;
                script = floatCurve.script;
                flags = floatCurve.flags;
            }
        }

        private readonly struct PPtrCurveKeyframe {
            public readonly float time;
            public readonly string value;
            
            public PPtrCurveKeyframe(float time, string value) {
                this.time = time;
                this.value = value;
            }
        }
        
        private readonly struct FloatCurveKeyframe {
            public readonly float time;
            public readonly int value;
            
            public FloatCurveKeyframe(float time, int value) {
                this.time = time;
                this.value = value;
            }
        }
    }
}
