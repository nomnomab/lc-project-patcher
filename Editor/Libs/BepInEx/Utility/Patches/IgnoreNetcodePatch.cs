#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil.Cil;
using UnityEngine;

namespace Patches {
    [HarmonyPatch]
    public static class IgnoreNetcodePatch {
        private static IEnumerable<MethodBase> TargetMethods() {
            var list = new List<MethodBase>();
            foreach (var pair in BepInExPatcher.GetAllPluginAssemblies(onlyMods: true)) {
                foreach (var module in pair.assembly.Modules) {
                    foreach (var type in module.Types) {
                        foreach (var method in type.Methods) {
                            var body = method.Body;
                            if (body == null) {
                                continue;
                            }
                            
                            var instructions = body.Instructions;
                            for (var i = 0; i < instructions.Count; i++) {
                                var instruction = instructions[i];
                                if (instruction.OpCode != OpCodes.Ldtoken) {
                                    continue;
                                }

                                var content = instruction.Operand.ToString();
                                if (!content.Contains("UnityEngine.RuntimeInitializeOnLoadMethodAttribute")) {
                                    continue;
                                }
                                
                                // check for System.Reflection.MethodBase::Invoke(object, object[])
                                var wasFound = false;
                                for (var j = i; j < instructions.Count; j++) {
                                    var invokeInstruction = instructions[j];
                                    if (invokeInstruction.OpCode != OpCodes.Callvirt) {
                                        continue;
                                    }
                                    
                                    if (!invokeInstruction.Operand.ToString().Contains("System.Reflection.MethodBase::Invoke(System.Object,System.Object[])")) {
                                        continue;
                                    }
                                    
                                    var methodBaseType = AccessTools.TypeByName(type.FullName);
                                    var methodBaseMethod = AccessTools.Method(methodBaseType, method.Name);
                                    // Debug.Log($"Found MethodBase method in \"{type.FullName}\" in \"{method.FullName}\" -> {methodBaseMethod?.Name ?? "null"}");

                                    if (methodBaseMethod == null) {
                                        Debug.LogWarning($"Failed to find MethodBase method in {type.FullName} in {method.FullName}");
                                        continue;
                                    }
                                    
                                    list.Add(methodBaseMethod);
                                    
                                    wasFound = true;
                                    break;
                                }

                                if (wasFound) {
                                    break;
                                }
                            }
                        }
                    }
                }
                pair.assembly.Dispose();
            }
            
            Debug.Log($"Found {list.Count} UnityEngine.RuntimeInitializeOnLoadMethodAttribute invokers");
            return list;
        }

        private static bool Prefix() {
            return false;
        }
    }
}
#endif
