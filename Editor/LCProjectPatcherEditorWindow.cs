using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher {
    public class LCProjectPatcherEditorWindow : EditorWindow {
        [MenuItem("Tools/Nomnom/LC - Project Patcher")]
        public static void ShowWindow() {
            GetWindow<LCProjectPatcherEditorWindow>("LC - Project Patcher");
        }

        private void CreateGUI() {
            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("MissingScriptValidator_Styles"));

            var scopeBox = new VisualElement();
            scopeBox.AddToClassList("scope-box");

            var scroll = new ScrollView();

            rootVisualElement.Add(scopeBox);

            scopeBox.Add(scroll);
            scroll.Add(CreateAssetRipperPathSelector());
            scroll.Add(CreateLethalCompanyDataPathSelector());
            
            var runButton = new Button(() => {
                SetCurrentStep(0);
                RunAll().Forget();
            }) {
                text = "Run Patcher"
            };
            scroll.Add(runButton);

            var label = new Label("Installation");
            label.name = "Title";
            scroll.Add(label);

            scroll.Add(CreateStep(string.Empty, () => InstallAll().Forget(), buttonText: "Install All"));
            scroll.Add(CreateStep("Update project settings", null, null));
            scroll.Add(CreateStep("Install required packages", null, null));
            // scroll.Add(CreateStep("Install required packages", () => Fix(PackagesModule.Patch).Forget()));
            scroll.Add(CreateStep("Copy required Lethal Company files", null, null));
            scroll.Add(CreateStep("Strip all Assembly-CSharp scripts", null, null));
            scroll.Add(CreateStep("Decompile the scripts",
                () => {
                    PreInit();
                    DecompiledScriptModule.Patch(true).Forget();
                }));

            label = new Label("Fixes");
            label.name = "Title";
            scroll.Add(label);

            scroll.Add(CreateStep(string.Empty, () => FixAll().Forget(), buttonText: "Fix All"));
            scroll.Add(CreateStep("Patch Scripts", null, null));
            scroll.Add(CreateStep("Patch Scriptable Objects", null, null));
            scroll.Add(CreateStep("Patch Materials", null, null));
            // scroll.Add(CreateStep("Patch Animation Clips", null, null));
            scroll.Add(CreateStep("Copy final Asset Ripper files", null, null));
            
            label = new Label("Post-Fixes");
            label.name = "Title";
            scroll.Add(label);
            
            scroll.Add(CreateStep(string.Empty, () => PostFixes().Forget(), buttonText: "Fix all"));
            scroll.Add(CreateStep("Patch scene list", null, null));
            scroll.Add(CreateStep("Patch HDRP volume profile", null, null));
            
            label = new Label("Optional");
            label.name = "Title";
            scroll.Add(label);
            
            scroll.Add(CreateStep("Open InitSceneLaunchOptions", FinalizerModule.OpenInitScene, "Open"));
            
            var foldout = new Foldout {
                text = "Debug tools",
                value = false,
                style = {
                    marginTop = new StyleLength(32)
                }
            };
            scroll.Add(foldout);
            foldout.Add(new Button(() => {
                GuidPatcherModule.Patch(debugMode: true).Forget();
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
            
            // var areStepsRunning = GetCurrentStep() != -1;
            // if (areStepsRunning) {
            //     RunAll().Forget();
            // } else {
            //     Debug.Log("No steps are running");
            // }
        }

        private static async UniTask Fix(Func<UniTask> task) {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            try {
                await task();
            } catch (Exception e) {
                Debug.LogException(e);
            }
            stopwatch.Stop();
            Debug.Log($"A task finished in {stopwatch.ElapsedMilliseconds}ms");
        }
        
        private static async UniTask<T> Fix<T>(Func<UniTask<T>> task) {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            T result = default;
            try {
                result = await task();
            } catch (Exception e) {
                Debug.LogException(e);
            }
            stopwatch.Stop();
            Debug.Log($"A task finished in {stopwatch.ElapsedMilliseconds}ms");
            return result;
        }

        private static bool ValidateAssetRipperPath() {
            var assetRipperPath = ModuleUtility.GetAssetRipperDirectory();
            if (string.IsNullOrEmpty(assetRipperPath)) {
                Debug.LogError("Asset Ripper path is not set");
                return false;
            } else if (!Directory.Exists(assetRipperPath)) {
                Debug.LogError($"Asset Ripper path does not exist: {assetRipperPath}");
                return false;
            } else if (!assetRipperPath.EndsWith("ExportedProject")) {
                Debug.LogError($"Asset Ripper path needs to end with the ExportedProject folder: {assetRipperPath}");
                return false;
            }

            return true;
        }

        private void SetWindowState(bool enabled, bool installedNewPackage = false) {
            if (enabled) {
                AssetDatabase.StopAssetEditing();
                rootVisualElement.SetEnabled(true);
                
                // ? re-open editor otherwise Unity is a bit stupid and won't reload packages until randomly later
                // ? this also forces the Input System backend pop-up to show up and actually do what it needs to
                if (installedNewPackage) {
                    EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                } else {
                    AssetDatabase.Refresh();
                    AssetDatabase.RefreshSettings();
                    ReimportRandomAsset();
                }
            } else {
                rootVisualElement.SetEnabled(false);
                AssetDatabase.StartAssetEditing();
            }
        }
        
        private static void PreInit() {
            // clone the asset ripper directory before working on it
            EditorUtility.DisplayProgressBar("Cloning Asset Ripper", "Creating Asset Ripper clone directory", 0.25f);
            var assetRipperPath = ModuleUtility.GetAssetRipperCloneDirectory();
            
            if (Directory.Exists(assetRipperPath)) {
                Directory.Delete(assetRipperPath, recursive: true);
            }
            
            EditorUtility.DisplayProgressBar("Cloning Asset Ripper", "Cloning Asset Ripper", 0.5f);
            ModuleUtility.CopyFilesRecursively(ModuleUtility.GetAssetRipperDirectory(), assetRipperPath);
            EditorUtility.ClearProgressBar();
        }
        
        private static int GetCurrentStep() {
            return EditorPrefs.GetInt("nomnom.lc_project_patcher.step", -1);
        }
        
        private static void SetCurrentStep(int step) {
            EditorPrefs.SetInt("nomnom.lc_project_patcher.step", step);
        }

        private async UniTask RunAll() {
            if (!ValidateAssetRipperPath()) {
                return;
            }
            
            // which step are we on
            var step = GetCurrentStep();
            switch (step) {
                case 0:
                    SetCurrentStep(step + 1);
                    await InstallAll();
                    break;
                case 1:
                    SetCurrentStep(step + 1);
                    await FixAll();
                    break;
                case 2:
                    SetCurrentStep(step + 1);
                    await PostFixes();
                    break;
                default:
                    SetCurrentStep(-1);
                    break;
            }

            step = GetCurrentStep();
            if (step == -1) return;
            
            // run the next step
            RunAll().Forget();
        }

        private async UniTask InstallAll() {
            if (!ValidateAssetRipperPath()) {
                return;
            }

            SetWindowState(false);
            PreInit();
            
            await Fix(ModifyProjectSettingsModule.Patch);
            var installedNewPackage = await Fix(PackagesModule.Patch);
            await Fix(SteamGameModule.Patch);
            await Fix(() => DecompiledScriptModule.Patch(false));
            await Fix(AssetRipperModule.PatchInstall);
            
            SetWindowState(true, installedNewPackage: installedNewPackage);
        }

        private async UniTask FixAll() {
            if (!ValidateAssetRipperPath()) {
                return;
            }
            
            SetWindowState(false);
            // PreInit();
            
            await Fix(() => GuidPatcherModule.Patch());
            // await Fix(AnimationClipPatcherModule.Patch);
            await Fix(AssetRipperModule.PatchFix);
            
            SetWindowState(true);
        }

        private async UniTask PostFixes() {
            if (!ValidateAssetRipperPath()) {
                return;
            }
            
            SetWindowState(false);
            // PreInit();
            
            await Fix(FinalizerModule.PatchSceneList);
            await Fix(FinalizerModule.PatchHDRPVolumeProfile);
            
            SetWindowState(true);
        }

        private static void ReimportRandomAsset() {
            var assets = AssetDatabase.FindAssets("t:Object");
            var randomAsset = assets[UnityEngine.Random.Range(0, assets.Length)];
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(randomAsset));
        }

        private static VisualElement CreateStep(string label, Action callback, string buttonText = "Fix") {
            var element = new VisualElement();
            element.AddToClassList("patch-step");
            element.Add(new Label(label));
            if (!string.IsNullOrEmpty(buttonText)) {
                element.Add(new Button(callback) {
                    text = buttonText
                });
            }
            return element;
        }

        private static VisualElement CreateAssetRipperPathSelector() {
            return CreatePathSelector("Asset Ripper", "nomnom.lc_project_patcher.asset_ripper_path");
        }

        private static VisualElement CreateLethalCompanyDataPathSelector() {
            return CreatePathSelector("Lethal Company Data", "nomnom.lc_project_patcher.lc_data_folder");
        }

        private static VisualElement CreatePathSelector(string name, string key) {
            var path = EditorPrefs.GetString(key);
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
