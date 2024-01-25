using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

            var label = new Label("Installation");
            label.name = "Title";
            scroll.Add(label);

            scroll.Add(CreateStep(string.Empty, () => InstallAll().Forget(), buttonText: "Install All"));
            scroll.Add(CreateStep("Update project settings", null, null));
            // scroll.Add(CreateStep("Update project settings", () => ModifyProjectSettingsModule.Patch()));
            scroll.Add(CreateStep("Install required packages", null, null));
            scroll.Add(CreateStep("Copy required Lethal Company files", null, null));
            scroll.Add(CreateStep("Strip all Assembly-CSharp scripts", null, null));

            label = new Label("Fixes");
            label.name = "Title";
            scroll.Add(label);

            scroll.Add(CreateStep(string.Empty, () => FixAll().Forget(), buttonText: "Fix All"));
            scroll.Add(CreateStep("Patch Scripts", null, null));
            scroll.Add(CreateStep("Patch Scriptable Objects", null, null));
            scroll.Add(CreateStep("Patch Materials", null, null));
            // scroll.Add(CreateStep("Patch Animation Clips", null, null));
            scroll.Add(CreateStep("Copy final Asset Ripper files", null, null));
            
            // scroll.Add(new Button(() => {
            //     GuidPatcherModule.Patch(debugMode: true).Forget();
            // }) {
            //     text = "Debug monoscripts"
            // });
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

        private async UniTask InstallAll() {
            rootVisualElement.SetEnabled(false);
            AssetDatabase.StartAssetEditing();
            
            await Fix(ModifyProjectSettingsModule.Patch);
            var installedNewPackage = await Fix(PackagesModule.Patch);
            await Fix(SteamGameModule.Patch);
            await Fix(AssetRipperModule.PatchInstall);
            
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
        }

        private async UniTask FixAll() {
            rootVisualElement.SetEnabled(false);
            AssetDatabase.StartAssetEditing();
            
            await Fix(() => GuidPatcherModule.Patch());
            // await Fix(AnimationClipPatcherModule.Patch);
            await Fix(AssetRipperModule.PatchFix);
            
            AssetDatabase.StopAssetEditing();
            rootVisualElement.SetEnabled(true);
            AssetDatabase.Refresh();
            AssetDatabase.RefreshSettings();
            ReimportRandomAsset();
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
