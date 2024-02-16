#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Patches {
    [HarmonyPatch]
    public static class IgnoreILHelpersPatch {
        private static IEnumerable<MethodBase> TargetMethods() {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                Type[] types;
                try {
                    types = assembly.GetTypes();
                } catch {
                    Debug.LogError($"Failed to get types from {assembly.FullName}");
                    continue;
                }
                
                foreach (var type in types) {
                    if (type == null) continue;

                    bool isPlugin;
                    try {
                        isPlugin = typeof(BaseUnityPlugin).IsAssignableFrom(type);
                    } catch {
                        Debug.LogError($"Failed to check if {type.FullName} is a BaseUnityPlugin");
                        continue;
                    }
                    
                    if (isPlugin) {
                        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
                        foreach (var method in methods) {
                            if (method.Name == "IlHook") {
                                yield return method;
                            }
                        }
                    }
                }
            }
        }

        private static bool Prefix() {
            return false;
        }
    }
}
#endif
