using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Editor.Modules;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor {
    public static class LCProjectPatcherSteps {
        public static Action onCompleted;
        public static bool IsWorking => _currentlyWorking;

        private static bool _currentlyWorking;
        
        [InitializeOnLoadMethod]
        private static void Initialize() {
            onCompleted = null;
        }
        
        public static async UniTaskVoid RunAll() {
            if (_currentlyWorking) {
                Debug.LogWarning("Already working on a step, please wait for it to finish");
                return;
            }
            
            _currentlyWorking = true;
            try {
                await RunStep();
            } catch (Exception e) {
                Debug.LogError($"Encountered an error when running step {GetCurrentStep()}!");
                Debug.LogException(e);
                ResetStep();
                try {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                } catch {
                    // ignored
                }
                throw;
            }
            finally {
                _currentlyWorking = false;
            }
        }

        private static void Reimport() {
            LCProjectPatcherUtility.ReimportRandomAsset();
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("Refreshed and saved");
        }

        private static async UniTask RunStep() {
            var step = GetCurrentStep();
            if (step == null) {
                return;
            }

            var settings = ModuleUtility.GetPatcherSettings();
            switch (step) {
                case 1: {
                    await RunPreProcessGroup(settings);
                    Reimport();
                    break;
                }
                case 2: {
                    await RunInstallGroup(settings);
                    break;
                }
                case 3: {
                    await RunGuidGroup(settings);
                    Reimport();
                    break;
                }
                case 4: {
                    await RunPostProcessGroup(settings);
                    Reimport();
                    break;
                }
                case 5: {
                    ResetStep();
                    onCompleted?.Invoke();
                    break;
                }
                default:
                    ResetStep();
                    break;
            }
        }

        public static async UniTask RunPreProcessGroup(LCPatcherSettings settings) {
            AssetDatabase.StartAssetEditing();

            try {
                InitialProjectModule.MoveNativeFiles(settings);

                // asset ripper
                await AssetRipperModule.RunAssetRipper(settings);
                AssetRipperModule.DeleteScriptsFromProject(settings);
                AssetRipperModule.DeleteScriptableObjectsFromProject(settings);

                var useCopy = false;
                DecompiledScriptModule.PatchAll(settings, useCopy);

                if (!useCopy) {
                    GuidPatcherModule.CreateES3DefaultsScript(settings);
                    
                    // steam files
                    SteamGameModule.CopyManagedDlls(settings);
                    SteamGameModule.CopyPluginDlls(settings);
                } else {
                    ResetStep();
                }
                
                // reimport AssetStore folder
                var assetStorePath = settings.GetAssetStorePath();
                if (AssetDatabase.IsValidFolder(assetStorePath)) {
                    AssetDatabase.ImportAsset(assetStorePath, ImportAssetOptions.ForceSynchronousImport);
                }
            } catch {
                ResetStep();
                Debug.LogError("Encountered an error when running the pre-process steps!");
                throw;
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }
            
            SetCurrentStep(2);
        }

        public static async UniTask RunInstallGroup(LCPatcherSettings settings) {
            AssetDatabase.StartAssetEditing();
            
            try {
                if (PackagesModule.InstallAll()) {
                    SetCurrentStep(3);
                    
                    EditorUtility.DisplayDialog("Packages installed", "Packages have been installed, please restart the editor", "Ok");
                    EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    //? delay to block the rest of the steps while it closes lol
                    await UniTask.Delay(100000);
                }
            } catch {
                ResetStep();
                Debug.LogError("Encountered an error when running the install steps!");
                throw;
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            SetCurrentStep(3);
            // RunAll().Forget();
        }

        public static UniTask RunGuidGroup(LCPatcherSettings settings) {
            GuidPatcherModule.PatchAll(settings);
            AssetRipperModule.RemoveDunGenFromOutputIfNeeded(settings);
            AssetRipperModule.CopyAssetRipperContents(settings);
            FinalizerModule.PatchES3DefaultsScriptableObject(settings);
            
            SetCurrentStep(4);
            return UniTask.CompletedTask;
        }

        public static async UniTask RunPostProcessGroup(LCPatcherSettings settings) {
            ModifyProjectSettingsModule.CopyOverProjectSettings();
            FinalizerModule.PatchSceneList(settings);
            FinalizerModule.PatchHDRPVolumeProfile(settings);
            FinalizerModule.PatchQualityPipelineAsset(settings);
            FinalizerModule.PatchDiageticAudioMixer(settings);
            FinalizerModule.SortScriptableObjectFolder(settings);
            FinalizerModule.SortPrefabsFolder(settings);
            FinalizerModule.OpenInitScene();
            FinalizerModule.ChangeGameViewResolution();
            
            InputActionsModule.FixAll(settings);

            BepInExModule.CopyTemplateFolder();
            await BepInExModule.Install(settings);
            BepInExModule.InstallMonoMod(settings);
            
            SetCurrentStep(5);   
            // return UniTask.CompletedTask;
        }
        
        private static string PatcherStepName => $"nomnom.lc_project_patcher.{Application.productName}.step";

        public static int? GetCurrentStep() {
            var step = EditorPrefs.GetInt(PatcherStepName, 0);
            return step <= 0 ? null : step;
        }
        
        public static void SetCurrentStep(int step) {
            EditorPrefs.SetInt(PatcherStepName, step);
            Debug.Log($"Step {step} has been set");
        }
        
        private static void ResetStep() {
            EditorPrefs.SetInt(PatcherStepName, 0);
            EditorPrefs.DeleteKey(PatcherStepName);
            Debug.Log("Step has been reset");
            EditorUtility.ClearProgressBar();
        }
    }
}
