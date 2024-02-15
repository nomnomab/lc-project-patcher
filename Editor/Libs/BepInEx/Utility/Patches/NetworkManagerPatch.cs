#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Patches {
    public static class NetworkManagerPatch {
        public static MethodBase TargetMethod() {
            var networkManagerType = AccessTools.TypeByName("Unity.Netcode.NetworkManager");
            var rpcFuncTableField = AccessTools.Field(networkManagerType, "__rpc_func_table");
            return rpcFuncTableField.FieldType.GetMethod("Add");
        }

        public static bool Prefix(IDictionary __instance, object[] __args) {
            var key = __args[0];
            var value = __args[1];
            if (__instance.Contains(key)) {
                var existingValue = __instance[key];
                Debug.LogWarning($"RPC function with key \"{key}\" already existed with value \"{existingValue}\"! Removing to prevent conflicts so \"{value}\" can be assigned.");
                // __instance.Remove(key);
                return false;
            }

            return true;
        }
    }
}
#endif
