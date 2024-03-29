﻿#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Patches {
    public static class IntroSkipperPatch {
        private static MethodBase TargetMethod() {
            // return AccessTools.Method("PreInitSceneScript:Start");
            return AccessTools.Method("PreInitSceneScript:SkipToFinalSetting");
        }
        
        private static void Postfix() {
            if (!ModuleUtility.GetPatcherRuntimeSettings().SkipIntro) return;
            Object.FindObjectOfType<BepInExPatcher>().StartCoroutine(Waiter());
        }
        
        private static IEnumerator Waiter() {
            // yield return new WaitForSeconds(0.5f + 0.2f);
            yield return SceneManager.LoadSceneAsync("InitSceneLANMode");
            yield return new WaitForSeconds(0.2f);
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }
    }

    [HarmonyPatch(typeof(MonoBehaviour))]
    public static class TestStartCoroutineRouterLLL {
        private static MonoBehaviour _runner;
        
        [HarmonyPatch(nameof(MonoBehaviour.StartCoroutine), new Type[] { typeof(IEnumerator) })]
        [HarmonyPrefix]
        private static bool Prefix(MonoBehaviour __instance, IEnumerator routine) {
            if (ModuleUtility.GetPatcherRuntimeSettings().DisablePreInitScriptCoroutineReplacer) {
                return true;
            }
            
            if (__instance.GetType().Name != "PreInitSceneScript") {
                return true;
            }
            
            // take the coroutine and say no
            if (!_runner) {
                var runner = new GameObject("[PreInitCoroutineRunner]");
                _runner = runner.AddComponent<Empty>();
                Object.DontDestroyOnLoad(_runner);
            }
            
            _runner.StartCoroutine(routine);
            Debug.Log("Started coroutine on runner.");
            return false;
        }
    }
    
    public static class MenuSkipper {
        private static MethodBase TargetMethod() {
            return AccessTools.Method("MenuManager:Start");
        }
        
        private static void Postfix(MonoBehaviour __instance) {
            if (!ModuleUtility.GetPatcherRuntimeSettings().SkipMainMenu) return;
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
            
            // set save file
            var settings = ModuleUtility.GetPatcherRuntimeSettings();
            // var gameNetworkManager = settings.GetGameNetworkManager();
            // var currentSaveFileName = gameNetworkManager.GetType().GetField("currentSaveFileName");
            // currentSaveFileName.SetValue(gameNetworkManager, settings.SaveFileIndex == -1 ? "LCChallengeFile" : $"LCSaveFile{settings.SaveFileIndex + 1}");
            var lobbyHostSettingsObj = GameObject.Find("LobbyHostSettings");
            var filesPanelObj = lobbyHostSettingsObj.transform.Find("FilesPanel");
            var files = filesPanelObj
                .GetComponentsInChildren<MonoBehaviour>()
                .Where(x => x.GetType().Name == "SaveFileUISlot")
                .ToArray();

            var saveFileIndex = settings.SaveFileIndex;
            if (saveFileIndex > files.Length) {
                saveFileIndex = files.Length - 1;
                Debug.LogWarning("Save file index out of range. Setting to last available save file.");
            }
            
            var wantedFile = files.FirstOrDefault(x => (int)(x.GetType().GetField("fileNum")?.GetValue(x) ?? -1) == saveFileIndex);
            if (!wantedFile) {
                Debug.LogError($"Failed to save file for index {saveFileIndex}!");
                yield break;
            }
            
            Debug.Log($"> Using {wantedFile}");

            if (settings.SaveFileResetBeforeLoad) {
                var deleteFileButtonObj = lobbyHostSettingsObj.transform.parent.Find("DeleteFileConfirmation/Panel/Delete");
                var deleteFileButton = deleteFileButtonObj
                    .GetComponents<MonoBehaviour>()
                    .FirstOrDefault(x => x.GetType().Name == "DeleteFileButton");
                deleteFileButton.GetType().GetMethod("SetFileToDelete").Invoke(deleteFileButton, new object[] {saveFileIndex});
                deleteFileButton.GetType().GetMethod("DeleteFile").Invoke(deleteFileButton, null);
                Debug.Log($"> Deleted save file {saveFileIndex}.");
            }

            // var saveFileNum = gameNetworkManager.GetType().GetField("saveFileNum");
            // saveFileNum.SetValue(gameNetworkManager, settings.SaveFileIndex == -1 ? -1 : settings.SaveFileIndex);

            wantedFile.GetType().GetMethod("SetFileToThis").Invoke(wantedFile, null);
            Debug.Log($"> Set save file to {saveFileIndex}.");
            
            yield return new WaitForSeconds(0.1f);
            
            var confirmHostButtonMethod = AccessTools.Method(menuManager.GetType(), "ConfirmHostButton");
            confirmHostButtonMethod.Invoke(menuManager, null);
        }
    }
}
#endif
