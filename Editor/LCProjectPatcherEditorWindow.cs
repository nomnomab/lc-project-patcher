using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Editor.Modules;
using Nomnom.LCProjectPatcher.Modules;
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
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Open")]
        public static void ShowWindow() {
            GetWindow<LCProjectPatcherEditorWindow>("LC - Project Patcher");
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory")]
        public static void UseGameBepInExDirectory() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_game_bepinex", false);
            EditorPrefs.SetBool("nomnom.lc_project_patcher.use_game_bepinex", !v);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory", v);
            
            EditorUtility.DisplayDialog("Restart Unity",
                "You may have to restart Unity to properly unload any loaded plugins since last changing this value!",
                "Ok");
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory", true)]
        public static bool UseGameBepInExDirectory_Bool() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_game_bepinex", false);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory", v);
            return true;
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
            
            var label = new Label("The location of the \"Lethal Company_Data\" folder");
            label.AddToClassList("section-label");
            scroll.Add(label);
            scroll.Add(CreateLethalCompanyDataPathSelector());
            
            label = new Label("Should the tool remove temp Asset Ripper files after patching?");
            label.AddToClassList("section-label");
            label.AddToClassList("gap-top");
            scroll.Add(label);
            
            var deleteTempAssetRipperFiles = new Toggle("Delete temp Asset Ripper files") {
                value = EditorPrefs.GetBool("nomnom.lc_project_patcher.delete_temp_asset_ripper_files", true)
            };
            deleteTempAssetRipperFiles.RegisterValueChangedCallback(evt => {
                EditorPrefs.SetBool("nomnom.lc_project_patcher.delete_temp_asset_ripper_files", evt.newValue);
            });
            scroll.Add(deleteTempAssetRipperFiles);

            label = new Label("Should the tool use the BepInEx folder from the game? (if not, it will use the one in the project)");
            label.AddToClassList("section-label");
            label.AddToClassList("gap-top");
            scroll.Add(label);
            
            var useGamesBepInExDirectory = new Toggle("Use the game's BepInEx folder") {
                value = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_game_bepinex", false)
            };
            useGamesBepInExDirectory.RegisterValueChangedCallback(x => {
                EditorPrefs.SetBool("nomnom.lc_project_patcher.use_game_bepinex", x.newValue);
                EditorUtility.DisplayDialog("Restart Unity",
                    "You may have to restart Unity to properly unload any loaded plugins since last changing this value!",
                    "Ok");
            });
            scroll.Add(useGamesBepInExDirectory);
            
            var lastPatchedAt = EditorPrefs.GetString("nomnom.lc_project_patcher.last_patched_at", "never");
            var lastPatchedAtLabel = new Label($"Last patched at: {lastPatchedAt}") {
                name = "last-patched-at"
            };
            rootVisualElement.Add(lastPatchedAtLabel);
            
            var runButton = new Button(() => {
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
                lastPatchedAtLabel.text = $"Last patched at: {date}";
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
            
            var foldout = new Foldout {
                text = "Debug tools",
                value = false,
                style = {
                    marginTop = new StyleLength(32)
                }
            };
            scroll.Add(foldout);
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
            projectPath.RegisterValueChangedCallback(x => {
                EditorPrefs.SetString(key, x.newValue);
            });
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
