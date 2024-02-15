// #if UNITY_EDITOR
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using HarmonyLib;
// using UnityEditor;
// using UnityEngine;
//
// namespace Patches {
//     [HarmonyPatch(typeof(Resources))]
//     public static class ResourcesPatch {
//         private static IEnumerable<MethodBase> TargetMethods() {
//             var genericMethod = AccessTools.Method(typeof(Resources), nameof(Resources.FindObjectsOfTypeAll));
//             var allUnityObjectTypes = AccessTools.AllTypes().Where(x => typeof(Object).IsAssignableFrom(x) && !x.IsAbstract && typeof(Object) != x);
//             foreach (var type in allUnityObjectTypes) {
//                 var method = genericMethod.MakeGenericMethod(type);
//                 yield return method;
//             }
//         }
//
//         private static void Prefix(MethodBase original, ref object __result) {
//             var type = original.GetGenericArguments().First();
//             __result = AssetDatabase.FindAssets($"t:{type.FullName}");
//         }
//     }
// }
// #endif
