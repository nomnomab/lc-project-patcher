#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Patches {
    [HarmonyPatch]
    public static class FindPluginTypesPatch {
        public static MethodBase TargetMethod() {
            return AccessTools
                .Method(typeof(TypeLoader), nameof(TypeLoader.FindPluginTypes))
                .MakeGenericMethod(typeof(PluginInfo));
        }

        public static Dictionary<string, List<PluginInfo>> Postfix(Dictionary<string, List<PluginInfo>> result) {
            var hasBepinPluginsFunction = AccessTools.Method(typeof(Chainloader), "HasBepinPlugins");
            foreach (var pair in BepInExPatcher.GetAllPluginAssemblies()) {
                if (result.ContainsKey(pair.file)) {
                    Debug.Log($"> Skipping \"{pair.file}\" as it has already been processed");
                    pair.assembly.Dispose();
                    continue;
                }
                
                if (!(bool)hasBepinPluginsFunction.Invoke(null, new object[] { pair.assembly })) {
                    result[pair.file] = new List<PluginInfo>();
                    pair.assembly.Dispose();
                    
                    Debug.Log($"No BepInEx plugins found in \"{pair.file}\"");
                    continue;
                }
                
                var list = pair.assembly.MainModule.Types
                    .Select(Chainloader.ToPluginInfo)
                    .Where(t => t != null)
                    .ToList();
                
                result[pair.file] = list;
                pair.assembly.Dispose();
                
                Debug.Log($"Found {list.Count} plugins in \"{pair.file}\"");

                for (var i = 0; i < list.Count; i++) {
                    var plugin = list[i];
                    Debug.Log($"- [{i}] {plugin.Metadata.GUID} - {plugin.Metadata.Name}");
                }
            }
            
            foreach (var pair in result) {
                Debug.Log($"- {pair.Key} has {pair.Value.Count} plugins");
                foreach (var plugin in pair.Value) {
                    Debug.Log($"  - {plugin.Metadata.GUID} - {plugin.Metadata.Name}");
                }
            }

            return result;
        }
    }
}
#endif
