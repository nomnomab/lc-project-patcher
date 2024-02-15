#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Patches {
    public static class IntroSkipperPatch {
        private static bool SkipMainMenu => ModuleUtility.GetPatcherRuntimeSettings().SkipMainMenu;
        
        private static MethodBase TargetMethod() {
            // return AccessTools.Method("PreInitSceneScript:Start");
            return AccessTools.Method("PreInitSceneScript:SkipToFinalSetting");
        }
        
        private static void Postfix() {
            if (!SkipMainMenu) return;
            Object.FindObjectOfType<BepInExPatcher>().StartCoroutine(Waiter());
        }
        
        private static IEnumerator Waiter() {
            // yield return new WaitForSeconds(0.5f + 0.2f);
            yield return SceneManager.LoadSceneAsync("InitSceneLANMode");
            yield return new WaitForSeconds(0.2f);
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }
    }
    
    public static class MenuSkipper {
        private static bool SkipMainMenu => ModuleUtility.GetPatcherRuntimeSettings().SkipMainMenu;
        
        private static MethodBase TargetMethod() {
            return AccessTools.Method("MenuManager:Start");
        }
        
        private static void Postfix(MonoBehaviour __instance) {
            if (!SkipMainMenu) return;
            __instance.StartCoroutine(Waiter());
        }
    
        private static IEnumerator Waiter() {
            yield return new WaitForSeconds(0.1f);
            var menuManagerObj = GameObject.Find("Canvas/MenuManager");
            var menuManager = menuManagerObj.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == "MenuManager");
            if (!menuManager) {
                Debug.LogError("Failed to find MenuManager!");
                yield break;
            }
            
            var clickHostButtonMethod = AccessTools.Method(menuManager.GetType(), "ClickHostButton");
            clickHostButtonMethod.Invoke(menuManager, null);
            
            yield return new WaitForSeconds(0.1f);
            
            var confirmHostButtonMethod = AccessTools.Method(menuManager.GetType(), "ConfirmHostButton");
            confirmHostButtonMethod.Invoke(menuManager, null);
        }
    }
}
#endif
