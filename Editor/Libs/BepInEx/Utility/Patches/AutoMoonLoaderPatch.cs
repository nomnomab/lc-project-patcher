#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEngine;

namespace Patches {
    [HarmonyPriority(priority: Priority.Last)]
    public static class AutoMoonLoaderPatch {
        public static MethodBase TargetMethod() {
            return AccessTools.Method("StartOfRound:Start");
        }
        
        public static void Postfix() {
            var settings = ModuleUtility.GetPatcherRuntimeSettings();
            if (!settings.AutoLoadMoon && string.IsNullOrEmpty(settings.AutoLoadMoonSceneName)) {
                return;
            }
            
            var startOfRound = settings.GetStartOfRound();
            if (startOfRound == null) return;

            startOfRound.StartCoroutine(LoadMoon());
        }
        
        private static IEnumerator LoadMoon() {
            yield return new WaitForSeconds(0.1f);
            
            var settings = ModuleUtility.GetPatcherRuntimeSettings();
            int levelId = 0;
            var startOfRound = settings.GetStartOfRound();

            if (settings.AutoLoadMoon) {
                levelId = (int)settings.AutoLoadMoon.GetType().GetField("levelID").GetValue(settings.AutoLoadMoon);
            } else if(!string.IsNullOrEmpty(settings.AutoLoadMoonSceneName)) {
                var levels = (Array)startOfRound.GetType().GetField("levels").GetValue(startOfRound);
                var found = false;
                foreach (var level in levels) {
                    var sceneName = (string)level.GetType().GetField("sceneName").GetValue(level);
                    if (sceneName == settings.AutoLoadMoonSceneName) {
                        levelId = (int)level.GetType().GetField("levelID").GetValue(level);
                        found = true;
                        break;
                    }
                }
                
                if (!found) {
                    Debug.LogError($"Failed to find level with scene name {settings.AutoLoadMoonSceneName}");
                    yield break;
                }
            } else {
                yield break;
            }
            
            var changeLevelFunction = AccessTools.Method("StartOfRound:ChangeLevel");
            var arriveAtLevelFunction = AccessTools.Method("StartOfRound:ArriveAtLevel");
            changeLevelFunction.Invoke(startOfRound, new object[] { levelId });
            arriveAtLevelFunction.Invoke(startOfRound, null);

            yield return new WaitForSeconds(0.1f);
            
            var pullLeverFunction = AccessTools.Method("StartOfRound:StartGame");
            pullLeverFunction.Invoke(startOfRound, null);
            Debug.Log("Auto loaded moon.");
        }
    }
}
#endif
