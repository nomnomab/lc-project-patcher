using System.IO;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor {
    public static class LCProjectPatcherSteps {
        public static async UniTaskVoid RunAll() {
            try {
                while (await RunStep()) {
                    var step = GetCurrentStep();
                    Debug.Log($"Step {step - 1} has been completed");
                }
            } catch {
                Debug.LogError($"Encountered an error when running step {GetCurrentStep()}!");
                ResetStep();
                throw;
            }
        }

        private static async UniTask<bool> RunStep() {
            var step = GetCurrentStep();
            if (step == null) {
                return false;
            }
            
            var settings = ModuleUtility.GetPatcherSettings();
            switch (step) {
                case 1: return await RunPreProcessGroup(settings);
                case 2: return await RunInstallGroup(settings);
                // case 3: return await RunGuidGroup();
                // case 4: return await RunPostProcessGroup();
                default:
                    ResetStep();
                    break;
            }

            return false;
        }
        
        public static async UniTask<bool> RunPreProcessGroup(LCPatcherSettings settings) {
            AssetDatabase.StartAssetEditing();

            try {
                InitialProjectModule.MoveNativeFiles(settings);

                // asset ripper
                await AssetRipperModule.RunAssetRipper(settings);
                // AssetRipperModule.CreateES3DefaultsScript(settings);

                // steam files
                SteamGameModule.CopyManagedDlls(settings);
                SteamGameModule.CopyPluginDlls(settings);
            } catch {
                ResetStep();
                Debug.LogError("Encountered an error when running the pre-process steps!");
                throw;
            }
            finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
            
            SetCurrentStep(2);
            return true;
        }

        public static async UniTask<bool> RunInstallGroup(LCPatcherSettings settings) {
            AssetDatabase.StartAssetEditing();
            
            try {
                if (await PackagesModule.InstallAll()) {
                    EditorUtility.DisplayDialog("Packages installed", "Packages have been installed, please restart the editor", "Ok");
                    EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    //? delay to block the rest of the steps while it closes lol
                    await UniTask.Delay(100000);
                    return false;
                }
            } catch {
                ResetStep();
                Debug.LogError("Encountered an error when running the install steps!");
                throw;
            }
            finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            SetCurrentStep(3);
            return true;
        }

        public static async UniTask<bool> RunGuidGroup() {
            SetCurrentStep(4);
            return true;
        }

        public static async UniTask<bool> RunPostProcessGroup() {
            SetCurrentStep(5);   
            return true;
        }

        public static int? GetCurrentStep() {
            var step = EditorPrefs.GetInt("nomnom.lc_project_patcher.step", 0);
            return step <= 0 ? null : step;
        }
        
        public static void SetCurrentStep(int step) {
            EditorPrefs.SetInt("nomnom.lc_project_patcher.step", step);
        }
        
        private static void ResetStep() {
            EditorPrefs.DeleteKey("nomnom.lc_project_patcher.step");
        }
    }
}
