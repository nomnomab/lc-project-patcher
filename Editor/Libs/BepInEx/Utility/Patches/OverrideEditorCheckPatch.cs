#if UNITY_EDITOR
using BepInEx.Bootstrap;
using HarmonyLib;

namespace Patches {
    [HarmonyPatch(typeof(Chainloader))]
    public static class OverrideEditorCheckPatch {
        [HarmonyPatch("IsEditor", MethodType.Getter)]
        [HarmonyPostfix]
        private static void Get(ref bool __result) {
            __result = false;
        }
    }
}
#endif
