using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class AssetBundleModule {
        public const string AssetBundleShaderPath = "Packages/com.nomnom.lc-project-patcher/Editor/Resources/Posterization/Dummy.shader";
        public static void CreateShaderBundle(string shaderName) {
            AssetBundleBuild assetBundleBuild = new();
            assetBundleBuild.assetBundleName = shaderName;
            assetBundleBuild.assetNames = new []{ AssetBundleShaderPath };
            
            BuildAssetBundlesParameters buildParameters = new() {
                outputPath = Application.temporaryCachePath,
                options = BuildAssetBundleOptions.ForceRebuildAssetBundle,
                bundleDefinitions = new []{ assetBundleBuild }
            };

            BuildPipeline.BuildAssetBundles(buildParameters);
        }
    }
}