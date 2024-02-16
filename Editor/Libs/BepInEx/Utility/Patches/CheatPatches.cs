#if UNITY_EDITOR
using System.Reflection;
using HarmonyLib;
using Nomnom.LCProjectPatcher.Editor.Modules;

namespace Patches {
    public static class InfiniteHealthPatch {
        public static MethodBase TargetMethod() {
            return AccessTools.Method("StartOfRound:Awake");
        }

        public static void Postfix() {
            var settings = ModuleUtility.GetPatcherRuntimeSettings();
            settings.HandleInfiniteHealth();
        }
    }
    
    public static class InfiniteStaminaPatch {
        private static readonly FieldInfo SprintMeter = AccessTools.TypeByName("PlayerControllerB").GetField("sprintMeter");
        
        public static MethodBase TargetMethod() {
            return AccessTools.Method("PlayerControllerB:Update");
        }

        public static void Postfix(object __instance) {
            var settings = ModuleUtility.GetPatcherRuntimeSettings();
            if (!settings.InfiniteStamina) return;
            
            var value = (float) SprintMeter.GetValue(__instance);
            if (value >= 1f) return;
            SprintMeter.SetValue(__instance, 1f);
        }
    }

    public static class CreditsPatch {
        private static readonly FieldInfo GroupCredits = AccessTools.TypeByName("Terminal").GetField("groupCredits");
        
        public static MethodBase TargetMethod() {
            return AccessTools.Method("Terminal:Start");
        }

        public static void Postfix() {
            var settings = ModuleUtility.GetPatcherRuntimeSettings();
            var terminal = settings.GetTerminal();
            if (terminal == null) return;
            if (settings.StartingCredits < 0) return;
            
            GroupCredits.SetValue(terminal, settings.StartingCredits);
        }
    }
}
#endif
