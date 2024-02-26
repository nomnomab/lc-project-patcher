#if UNITY_EDITOR
using System;
using System.Reflection;
using HarmonyLib;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEngine;

namespace Patches {
    public static class SkipTerminalIntroPatch {
        private static MethodBase TargetMethod() {
            // return AccessTools.Method("PreInitSceneScript:Start");
            return AccessTools.Method("Terminal:BeginUsingTerminal");
        }
        
        private static void Prefix(object __instance) {
            if (!ModuleUtility.GetPatcherRuntimeSettings().SkipTerminalIntro) return;

            try {
                var es3Save = AccessTools.Method("ES3:Save", new[] { typeof(string), typeof(object), typeof(string) });
                es3Save.Invoke(null, new object[] { "HasUsedTerminal", true, "LCGeneralSaveData" });
            } catch(Exception e) {
                Debug.LogError("Failed to save HasUsedTerminal to ES3.");
                Debug.LogException(e);
            }

            try {
                var usedTerminalThisSession = __instance.GetType().GetField("usedTerminalThisSession", BindingFlags.NonPublic | BindingFlags.Instance);
                usedTerminalThisSession.SetValue(__instance, true);
            } catch(Exception e) {
                Debug.LogError("Failed to set usedTerminalThisSession to true.");
                Debug.LogException(e);
            }
        }
    }
}
#endif
