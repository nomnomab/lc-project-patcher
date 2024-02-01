using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor {
    public static class LCProjectPatcherUtility {
        public static async UniTask Fix(Func<UniTask> task) {
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
        
        public static async UniTask<T> Fix<T>(Func<UniTask<T>> task) {
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
        
        public static void ReimportRandomAsset() {
            var assets = AssetDatabase.FindAssets("t:Object");
            var randomAsset = assets[UnityEngine.Random.Range(0, assets.Length)];
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(randomAsset));
        }
        
        // public static bool ValidateAssetRipperPath() {
        //     var assetRipperPath = ModuleUtility.GetAssetRipperDirectory();
        //     if (string.IsNullOrEmpty(assetRipperPath)) {
        //         Debug.LogError("Asset Ripper path is not set");
        //         return false;
        //     } else if (!Directory.Exists(assetRipperPath)) {
        //         Debug.LogError($"Asset Ripper path does not exist: {assetRipperPath}");
        //         return false;
        //     } else if (!assetRipperPath.EndsWith("ExportedProject")) {
        //         Debug.LogError($"Asset Ripper path needs to end with the ExportedProject folder: {assetRipperPath}");
        //         return false;
        //     }
        //
        //     return true;
        // }
    }
}
