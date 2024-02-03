using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Nomnom.LCProjectPatcher.Editor {
    public class LCProjectPatcherEditorWindow : EditorWindow {
        public static LCProjectPatcherEditorWindow Instance { get; private set; }
        
        private static LCProjectPatcherEditorWindow _instance;
        private int _lastStep;
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher")]
        public static void ShowWindow() {
            GetWindow<LCProjectPatcherEditorWindow>("LC - Project Patcher");
        }

        private void CreateGUI() {
            if (_instance && _instance != this) {
                Close();
                return;
            }
            
            // validate that folders exists
            var settings = ModuleUtility.GetPatcherSettings();
            ModuleUtility.CreateDirectory(settings.GetBaseUnityPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetBaseLethalCompanyPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetNativePath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetAssetStorePath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetModsPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetToolsPath(fullPath: true));
            
            AssetDatabase.Refresh();
            
            _instance = this;
            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("MissingScriptValidator_Styles"));

            var scopeBox = new VisualElement();
            scopeBox.AddToClassList("scope-box");

            var scroll = new ScrollView();

            rootVisualElement.Add(scopeBox);

            scopeBox.Add(scroll);
            scroll.Add(CreateLethalCompanyDataPathSelector());
            
            var deleteTempAssetRipperFiles = new Toggle("Delete temp Asset Ripper files") {
                value = EditorPrefs.GetBool("nomnom.lc_project_patcher.delete_temp_asset_ripper_files", true)
            };
            deleteTempAssetRipperFiles.RegisterValueChangedCallback(evt => {
                EditorPrefs.SetBool("nomnom.lc_project_patcher.delete_temp_asset_ripper_files", evt.newValue);
            });
            scroll.Add(deleteTempAssetRipperFiles);
            
            var runButton = new Button(() => {
                // validate data path
                var dataPath = ModuleUtility.LethalCompanyDataFolder;
                if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath)) {
                    Debug.LogError("Lethal Company data path is invalid!");
                    return;
                }
                
                if (!dataPath.EndsWith("_Data")) {
                    var folderName = Path.GetFileNameWithoutExtension(dataPath);
                    var dataFolder = $"{folderName}_Data";
                    dataPath = Path.Combine(dataPath, dataFolder);

                    if (!Directory.Exists(dataPath)) {
                        Debug.LogError("The data path needs to end in \"_Data\"!");
                        return;
                    }
                }

                if (!EditorUtility.DisplayDialog("Run Patcher", "Are you sure you want to run the patcher? This will modify your project. Make sure you keep the editor focused while it works.", "Yes", "No")) {
                    return;
                }
                
                SetWindowState(false);
                LCProjectPatcherSteps.SetCurrentStep(1);
                _lastStep = 1;
                LCProjectPatcherSteps.RunAll().Forget();
            }) {
                text = "Run Patcher",
                style = {
                    height = 24
                }
            };
            scroll.Add(runButton);

            // var label = new Label("Installation");
            // label.name = "Title";
            // scroll.Add(label);
            
            // scroll.Add(CreateStep("Decompile the game's assets",
            //     () => {
            //         // AssetRipperModule.RunAssetRipper().Forget();
            //     }));
            // scroll.Add(CreateStep("Copy required Lethal Company files", null));
            // scroll.Add(CreateStep("Strip all Assembly-CSharp scripts", null));
            // scroll.Add(CreateStep("Patch the scripts",
            //     () => {
            //         // PreInit();
            //         // DecompiledScriptModule.Patch(true).Forget();
            //     }));
            // scroll.Add(CreateStep("Install required packages", null));
            //
            // label = new Label("Fixes");
            // label.name = "Title";
            // scroll.Add(label);
            //
            // scroll.Add(CreateStep("Patch Scripts", null));
            // scroll.Add(CreateStep("Patch Scriptable Objects", null));
            // scroll.Add(CreateStep("Patch Materials", null));
            // scroll.Add(CreateStep("Generate editor scripts", null));
            // scroll.Add(CreateStep("Copy final Asset Ripper files", null));
            //
            // label = new Label("Post-Fixes");
            // label.name = "Title";
            // scroll.Add(label);
            //
            // scroll.Add(CreateStep("Update project settings", null));
            // scroll.Add(CreateStep("Patch scene list", null));
            // scroll.Add(CreateStep("Patch HDRP volume profile", null));
            // scroll.Add(CreateStep("Restructure project", null));
            //
            // label = new Label("Optional");
            // label.name = "Title";
            // scroll.Add(label);
            //
            // scroll.Add(CreateStep("Open InitSceneLaunchOptions", FinalizerModule.OpenInitScene, "Open"));
            
            var foldout = new Foldout {
                text = "Debug tools",
                value = false,
                style = {
                    marginTop = new StyleLength(32)
                }
            };
            scroll.Add(foldout);
            foldout.Add(new Button(() => {
                // GuidPatcherModule.Patch(debugMode: true).Forget();
            }) {
                text = "Debug monoscripts"
            });
            
            var objField = new ObjectField("ObjectField") {
                objectType = typeof(Object)
            };
            foldout.Add(objField);
            
            var findGuidButton = new Button(() => {
                var obj = objField.value;
                var globalID = GlobalObjectId.GetGlobalObjectIdSlow(obj);
                Debug.Log(globalID);
            }) {
                text = "Find GUID"
            };
            foldout.Add(findGuidButton);
            
            var clearPrefs = new Button(() => {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }) {
                text = "Clear Prefs"
            };
            foldout.Add(clearPrefs);
            
            var diagetic = new Button(() => {
                FinalizerModule.PatchDiageticAudioMixer(ModuleUtility.GetPatcherSettings());
            }) {
                text = "Diagetic"
            };
            foldout.Add(diagetic);

            LCProjectPatcherSteps.onCompleted += () => {
                SetWindowState(true);
                Debug.Log("Patcher has completed :)");
                
                if (EditorPrefs.GetBool("nomnom.lc_project_patcher.delete_temp_asset_ripper_files", true)) {
                    var assetRipperPath = ModuleUtility.AssetRipperTempDirectory;
                    try {
                        if (Directory.Exists(assetRipperPath)) {
                            Directory.Delete(assetRipperPath, recursive: true);
                        }
                    
                        Debug.Log("Deleted temp Asset Ripper files");
                    } catch (Exception e) {
                        Debug.LogError($"Failed to delete temp Asset Ripper files: {e}");
                    }
                }
            };
            // if (LCProjectPatcherSteps.GetCurrentStep() is {} step && step > 0) {
            //     SetWindowState(false);
            //     LCProjectPatcherSteps.RunAll().Forget();
            // }
        }

        public void SetWindowState(bool enabled) {
            rootVisualElement.SetEnabled(true);
        }

        private void Update() {
            if (LCProjectPatcherSteps.GetCurrentStep() is {} step && step != _lastStep) {
                if (LCProjectPatcherSteps.IsWorking) return;
                _lastStep = step;
                Debug.Log($"Step {step}");
                SetWindowState(false);
                LCProjectPatcherSteps.RunAll().Forget();
            }
        }

        // private static void PreInit() {
        //     // clone the asset ripper directory before working on it
        //     EditorUtility.DisplayProgressBar("Cloning Asset Ripper", "Creating Asset Ripper clone directory", 0.25f);
        //     var assetRipperPath = ModuleUtility.GetAssetRipperCloneDirectory();
        //     
        //     if (Directory.Exists(assetRipperPath)) {
        //         Directory.Delete(assetRipperPath, recursive: true);
        //     }
        //     
        //     EditorUtility.DisplayProgressBar("Cloning Asset Ripper", "Cloning Asset Ripper", 0.5f);
        //     ModuleUtility.CopyFilesRecursively(ModuleUtility.GetAssetRipperDirectory(), assetRipperPath);
        //     EditorUtility.ClearProgressBar();
        // }

        // private async UniTask RunAll() {
        //     if (!ValidateAssetRipperPath()) {
        //         return;
        //     }
        //     
        //     // which step are we on
        //     var step = GetCurrentStep();
        //     switch (step) {
        //         case 0:
        //             SetCurrentStep(step + 1);
        //             await InstallAll();
        //             break;
        //         case 1:
        //             SetCurrentStep(step + 1);
        //             await FixAll();
        //             break;
        //         case 2:
        //             SetCurrentStep(step + 1);
        //             await PostFixes();
        //             break;
        //         default:
        //             SetCurrentStep(-1);
        //             break;
        //     }
        //
        //     step = GetCurrentStep();
        //     if (step == -1) return;
        //     
        //     // run the next step
        //     RunAll().Forget();
        // }

        // private async UniTask InstallAll() {
        //     if (!ValidateAssetRipperPath()) {
        //         return;
        //     }
        //
        //     SetWindowState(false);
        //     PreInit();
        //     
        //     await Fix(ModifyProjectSettingsModule.Patch);
        //     var installedNewPackage = await Fix(PackagesModule.Patch);
        //     await Fix(SteamGameModule.Patch);
        //     await Fix(() => DecompiledScriptModule.Patch(false));
        //     await Fix(AssetRipperModule.PatchInstall);
        //     
        //     SetWindowState(true, installedNewPackage: installedNewPackage);
        // }
        //
        // private async UniTask FixAll() {
        //     if (!ValidateAssetRipperPath()) {
        //         return;
        //     }
        //     
        //     SetWindowState(false);
        //     // PreInit();
        //     
        //     await Fix(() => GuidPatcherModule.Patch());
        //     // await Fix(AnimationClipPatcherModule.Patch);
        //     await Fix(AssetRipperModule.PatchFix);
        //     
        //     SetWindowState(true);
        // }
        //
        // private async UniTask PostFixes() {
        //     if (!ValidateAssetRipperPath()) {
        //         return;
        //     }
        //     
        //     SetWindowState(false);
        //     // PreInit();
        //     
        //     await Fix(FinalizerModule.PatchSceneList);
        //     await Fix(FinalizerModule.PatchHDRPVolumeProfile);
        //     
        //     SetWindowState(true);
        // }

        private static VisualElement CreateStep(string label, Action callback, string buttonText = "Fix") {
            var element = new VisualElement();
            element.AddToClassList("patch-step");
            element.Add(new Label(label));
            if (callback != null && !string.IsNullOrEmpty(buttonText)) {
                element.Add(new Button(callback) {
                    text = $"dev: {buttonText}"
                });
            }
            return element;
        }

        private static VisualElement CreateLethalCompanyDataPathSelector() {
            return CreatePathSelector("Lethal Company Data", "nomnom.lc_project_patcher.lc_data_folder", "C:/Program Files (x86)/Steam/steamapps/common/Lethal Company/Lethal Company_Data".Replace('/', Path.DirectorySeparatorChar));
        }

        private static VisualElement CreatePathSelector(string name, string key, string defaultValue) {
            var path = EditorPrefs.GetString(key, defaultValue);
            var pathHorizontal = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row
                }
            };
            pathHorizontal.name = "PathHorizontal";
            var projectPath = new TextField($"{name} path") {
                value = path,
                multiline = false,
                isDelayed = true
            };
            var browseButton = new Button(() => {
                var newPath = EditorUtility.OpenFolderPanel($"Select {name} Path", path, "");
                if (string.IsNullOrEmpty(newPath)) {
                    return;
                }

                projectPath.value = newPath;
                EditorPrefs.SetString(key, newPath);
            }) {
                text = "Browse"
            };
            pathHorizontal.Add(projectPath);
            pathHorizontal.Add(browseButton);
            return pathHorizontal;
        }
    }
}
