using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Editor.Modules;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher.Editor {
    public class LCProjectPatcherEditorWindow : EditorWindow {
        public static LCProjectPatcherEditorWindow Instance { get; private set; }
        
        private static LCProjectPatcherEditorWindow _instance;
        private int _lastStep;

        private void CreateGUI() {
            if (_instance && _instance != this) {
                Close();
                return;
            }
            
            // validate that folders exists
            var settings = ModuleUtility.GetPatcherSettings();
            _ = ModuleUtility.GetPatcherRuntimeSettings();
            ModuleUtility.CreateDirectory(settings.GetBaseUnityPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetBaseLethalCompanyPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetNativePath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetAssetStorePath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetModsPath(fullPath: true));
            ModuleUtility.CreateDirectory(settings.GetToolsPath(fullPath: true));
            
            AssetDatabase.Refresh();
            
            _instance = this;
            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("MissingScriptValidator_Styles"));

            var mainDocument = Resources.Load<VisualTreeAsset>("LCPatcher_Main");
            mainDocument.CloneTree(rootVisualElement);
            
            const string lastPatchedAtKey = "nomnom.lc_project_patcher.last_patched_at";
            var lastPatchedAt = EditorPrefs.GetString(lastPatchedAtKey, "never");
            var lastPatchedAtLabel = rootVisualElement.Q<Label>("last-patched-at");
            lastPatchedAtLabel.text = $"last patched at: {lastPatchedAt}";

            const string dataFolderPathKey = "nomnom.lc_project_patcher.lc_data_folder";
            var dataPath = EditorPrefs.GetString(dataFolderPathKey, "C:/Program Files (x86)/Steam/steamapps/common/Lethal Company/Lethal Company_Data".Replace('/', Path.DirectorySeparatorChar));
            var lethalCompanyDataPath = rootVisualElement.Q<TextField>("lc-data-path-input");
            lethalCompanyDataPath.value = dataPath;
            
            var lethalCompanyDataPathBrowseButton = rootVisualElement.Q<Button>();
            
            lethalCompanyDataPath.RegisterValueChangedCallback(x => {
                EditorPrefs.SetString(dataFolderPathKey, x.newValue);
            });
            
            lethalCompanyDataPathBrowseButton.clicked += () => {
                var newPath = EditorUtility.OpenFolderPanel("Select Lethal Company Data Path", dataPath, "");
                if (string.IsNullOrEmpty(newPath)) {
                    return;
                }

                lethalCompanyDataPath.value = newPath;
                EditorPrefs.SetString(dataFolderPathKey, newPath);
            };
            
            const string deleteTempAssetRipperFilesKey = "nomnom.lc_project_patcher.delete_temp_asset_ripper_files";
            var deleteTempAssetRipperFiles = rootVisualElement.Q<Toggle>("delete-temp-ripper-files-toggle");
            deleteTempAssetRipperFiles.value = EditorPrefs.GetBool(deleteTempAssetRipperFilesKey, true);
            deleteTempAssetRipperFiles.RegisterValueChangedCallback(evt => {
                EditorPrefs.SetBool(deleteTempAssetRipperFilesKey, evt.newValue);
            });
            
            const string useGameBepInExKey = "nomnom.lc_project_patcher.use_game_bepinex";
            var useGamesBepInExDirectory = rootVisualElement.Q<Toggle>("use-game-bepinex-toggle");
            useGamesBepInExDirectory.value = EditorPrefs.GetBool(useGameBepInExKey, false);
            useGamesBepInExDirectory.RegisterValueChangedCallback(x => {
                EditorPrefs.SetBool(useGameBepInExKey, x.newValue);
                EditorUtility.DisplayDialog("Restart Unity",
                    "You may have to restart Unity to properly unload any loaded plugins since last changing this value!",
                    "Ok");
            });
            
            var patcherButton = rootVisualElement.Q<Button>("patch-button");
            patcherButton.clicked += RunPatcher;
            patcherButton.clicked += () => {
                var lastPatchedAt = EditorPrefs.GetString(lastPatchedAtKey, "never");
                lastPatchedAtLabel.text = $"last patched at: {lastPatchedAt}";
            };
            
            // CreateDebugFoldout(rootVisualElement.Q("scroll"));
            CreateUtilityFoldout(rootVisualElement.Q("utilities"));
            
            LCProjectPatcherSteps.onCompleted += () => {
                SetWindowState(true);
                Debug.Log("Patcher has completed :)");
                
                if (EditorPrefs.GetBool(deleteTempAssetRipperFilesKey, true)) {
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
        }

        private void CreateUtilityFoldout(VisualElement parent) {
            var utilitiesFoldout = (Foldout)parent;
            utilitiesFoldout.value = false;
            
            var packageList = parent.Q("package-list");
            packageList.Clear();

            utilitiesFoldout.RegisterValueChangedCallback(x => {
                if (x.newValue) {
                    packageList.Clear();
                    
                    var packages = PackagesModule.Packages;
                    var gitPackages = PackagesModule.GitPackages;

                    var installedPackages = Client.List(false, false);
                    while (!installedPackages.IsCompleted) {
                        if (installedPackages.Error != null) {
                            Debug.LogError($"Failed to get installed packages: {installedPackages.Error}");
                            break;
                        }
                    }

                    foreach (var (packageName, version) in packages) {
                        var packageString = version == null ? packageName : $"{packageName}@{version}";
                        var isInstalled = installedPackages.Result.Any(x => x.packageId == packageString);

                        var packageNameLabel = new Label(packageName);
                        packageNameLabel.AddToClassList("package-label");

                        var packageVersionLabel = new Label($"{version} ({(isInstalled ? "installed" : "not installed")})");
                        packageVersionLabel.AddToClassList("package-version");

                        packageList.Add(packageNameLabel);
                        packageList.Add(packageVersionLabel);
                    }

                    foreach (var gitPackage in gitPackages) {
                        var isInstalled = installedPackages.Result.Any(x => x.packageId.EndsWith(gitPackage));

                        var packageNameLabel = new Label(gitPackage);
                        packageNameLabel.AddToClassList("package-label");
                        var packageVersionLabel = new Label($"(git) ({(isInstalled ? "installed" : "not installed")})");
                        packageVersionLabel.AddToClassList("package-version");

                        packageList.Add(packageNameLabel);
                        packageList.Add(packageVersionLabel);
                    }
                }
            });
            
            var runAssetRipperButton = parent.Q("run-asset-ripper").Q<Button>();
            var installPackagesButton = parent.Q("install-packages").Q<Button>();
            var fixMixerButton = parent.Q("fix-mixer").Q<Button>();
            var fixInputActionsButton = parent.Q("fix-input-actions").Q<Button>();
            var sortPrefabsButtons = parent.Q("sort-prefabs").Query<Button>().ToList();
            var sortScriptableObjectsButtons = parent.Q("sort-sos").Query<Button>().ToList();
            
            runAssetRipperButton.clicked += () => {
                AssetRipperModule.RunAssetRipper(ModuleUtility.GetPatcherSettings()).Forget();
            };
            
            installPackagesButton.clicked += () => {
                PackagesModule.InstallAll();
            };
            
            fixMixerButton.clicked += () => {
                FinalizerModule.PatchDiageticAudioMixer(ModuleUtility.GetPatcherSettings());
            };
            
            fixInputActionsButton.clicked += () => {
                InputActionsModule.FixAll(ModuleUtility.GetPatcherSettings());
            };
            
            sortPrefabsButtons[0].clicked += () => {
                FinalizerModule.SortPrefabsFolder(ModuleUtility.GetPatcherSettings());
                AssetDatabase.Refresh();
            };
            
            sortPrefabsButtons[1].clicked += () => {
                FinalizerModule.UnSortPrefabsFolder(ModuleUtility.GetPatcherSettings());
                AssetDatabase.Refresh();
            };
            
            sortScriptableObjectsButtons[0].clicked += () => {
                FinalizerModule.SortScriptableObjectFolder(ModuleUtility.GetPatcherSettings());
                AssetDatabase.Refresh();
            };
            
            sortScriptableObjectsButtons[1].clicked += () => {
                FinalizerModule.UnSortScriptableObjectFolder(ModuleUtility.GetPatcherSettings());
                AssetDatabase.Refresh();
            };
        }

        private void CreateDebugFoldout(VisualElement parent) {
            var foldout = new Foldout {
                text = "Debug tools",
                value = false
            };
            parent.Add(foldout);
            foldout.Add(new Button(() => {
                GuidPatcherModule.PatchAll(ModuleUtility.GetPatcherSettings(), debugMode: true);
            }) {
                text = "Debug monoscripts"
            });
            foldout.Add(new Button(() => {
                AssetRipperModule.RemoveDunGenFromOutputIfNeeded(ModuleUtility.GetPatcherSettings());
            }) {
                text = "Test DunGen path"
            });
            foldout.Add(new Button(() => {
                AssetRipperModule.RunAssetRipper(ModuleUtility.GetPatcherSettings()).Forget();
            }) {
                text = "Run Asset Ripper"
            });
            foldout.Add(new Button(() => {
                FinalizerModule.SortScriptableObjectFolder(ModuleUtility.GetPatcherSettings());
                AssetDatabase.Refresh();
            }) {
                text = "Sort ScriptableObjects"
            });
            foldout.Add(new Button(() => {
                FinalizerModule.UnSortScriptableObjectFolder(ModuleUtility.GetPatcherSettings());
                AssetDatabase.Refresh();
            }) {
                text = "Unsort ScriptableObjects"
            });
            foldout.Add(new Button(() => {
                FinalizerModule.SortPrefabsFolder(ModuleUtility.GetPatcherSettings());
                AssetDatabase.Refresh();
            }) {
                text = "Sort Prefabs"
            });
            foldout.Add(new Button(() => {
                FinalizerModule.UnSortPrefabsFolder(ModuleUtility.GetPatcherSettings());
            }) {
                text = "Unsort Prefabs"
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
                text = "Fix Diagetic Mixer"
            };
            foldout.Add(diagetic);
        }

        private void RunPatcher() {
            // validate data path
            var dataPath = ModuleUtility.LethalCompanyDataFolder;
            if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath)) {
                Debug.LogError("Lethal Company data path is invalid!");
                return;
            }
            
            if (!dataPath.EndsWith("_Data")) {
                Debug.LogError("The data path needs to end in \"_Data\"!");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Run Patcher", "Are you sure you want to run the patcher? This will modify your project. Make sure you keep the editor focused while it works.", "Yes", "No")) {
                return;
            }
            
            SetWindowState(false);
            var date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            EditorPrefs.SetString("nomnom.lc_project_patcher.last_patched_at", date);
            LCProjectPatcherSteps.SetCurrentStep(1);
            _lastStep = 1;
            LCProjectPatcherSteps.RunAll().Forget();
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
    }
}
