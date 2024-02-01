using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class PackagesModule {
        private readonly static (string, string)[] Packages = new[] {
            ("com.unity.ai.navigation", "1.1.5"),
            ("com.unity.animation.rigging", "1.2.1"),
            ("com.unity.collections", "1.2.4"),
            ("com.unity.netcode.gameobjects", "1.5.2"),
            ("com.unity.probuilder", "5.1.1"),
            ("com.unity.inputsystem", "1.7.0"),
        };

        private readonly static string[] GitPackages = new[] {
            "https://github.com/Unity-Technologies/AssetBundles-Browser.git",
            "https://github.com/v0lt13/EditorAttributes.git"
        };
        
        public static async UniTask<bool> InstallAll() {
            ImportTMP();
            
            var packageStrings = Packages
                .Select(x => x.Item2 == null ? x.Item1 : $"{x.Item1}@{x.Item2}")
                .ToArray();
            var allPackageStrings = packageStrings
                .Concat(GitPackages)
                .ToArray();
            
            // check if packages are already installed
            EditorUtility.DisplayProgressBar("Installing packages", "Checking if packages are already installed", 0.25f);
            try {
                Debug.Log("Checking if packages are already installed");
                var installedPackages = Client.List(false, false);
                while (!installedPackages.IsCompleted) {
                    // await UniTask.Delay(1, ignoreTimeScale: true);
                }
                
                var allAreInstalled = packageStrings.All(x => installedPackages.Result.Count(y => y.packageId == x) > 0);
                if (allAreInstalled) {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("Packages already installed");
                    return false;
                }
            
                EditorUtility.DisplayProgressBar("Installing packages", $"Installing {allPackageStrings.Length} package{(allPackageStrings.Length == 1 ? string.Empty : "s")}", 0.5f);
                var request = Client.AddAndRemove(allPackageStrings);
                while (!request.IsCompleted) {
                    // await UniTask.Delay(1, ignoreTimeScale: true);
                }
            
                Client.Resolve();
                EditorUtility.ClearProgressBar();
            } catch(Exception e) {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Failed to get installed packages: {e}");
                return false;
            }
            
            Debug.Log("Packages installed");
            return true;
        }

        private static void ImportTMP() {
            // import the TMP package automatically
            EditorUtility.DisplayProgressBar("Installing packages", "Installing TMP Essential Resources", 1f);
            AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
            EditorUtility.ClearProgressBar();
        }
    }
}
