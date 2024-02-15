#if UNITY_EDITOR
using System.Reflection;
using HarmonyLib;
using UnityEditor;

namespace Patches {
    public static class OnlineDisablerPatch {
        private static MethodBase TargetMethod() {
            return AccessTools.Method("PreInitSceneScript:ChooseLaunchOption");
        }
        
        private static bool Prefix(bool online) {
            if (online) {
                EditorUtility.DisplayDialog("Error", "Online mode is not supported!", "OK");
                return false;
            }
    
            return true;
        }
    }
}
#endif
