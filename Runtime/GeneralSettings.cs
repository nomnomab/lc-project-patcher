using System;
using System.IO;
using Nomnom.LCProjectPatcher.Attributes;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    [Serializable]
    public class GeneralSettings {
        [SerializedPath(nameof(LCPatcherSettings.GetBaseUnityPath))]
        [SerializeField] 
        private string _nativePath = "Native";
        
        [SerializedPath(nameof(LCPatcherSettings.GetBaseUnityPath))]
        [SerializeField] 
        private string _assetStorePath = "AssetStore";
        
        [SerializedPath(nameof(LCPatcherSettings.GetBaseLethalCompanyPath))]
        [SerializeField] 
        private string _modsPath = "Mods";
        
        [SerializedPath(nameof(LCPatcherSettings.GetBaseLethalCompanyPath))]
        [SerializeField] 
        private string _toolsPath = "Tools";
        
        [SerializedPath(nameof(LCPatcherSettings.GetBaseLethalCompanyPath))]
        [SerializeField] 
        private string _resourcesPath = "Resources";

        [SerializedPath(nameof(LCPatcherSettings.GetBaseLethalCompanyPath))]
        [SerializeField] 
        private string _streamingAssetsPath = "StreamingAssets";

        public string GetNativePath(string path) {
            return Path.Combine(path, _nativePath);
        }
        
        public string GetAssetStorePath(string path) {
            return Path.Combine(path, _assetStorePath);
        }
        
        public string GetModsPath(string path) {
            return Path.Combine(path, _modsPath);
        }
        
        public string GetToolsPath(string path) {
            return Path.Combine(path, _toolsPath);
        }
        
        public string GetResourcesPath(string path) {
            return Path.Combine(path, _resourcesPath);
        }
        
        public string GetStreamingAssetsPath(string path) {
            return Path.Combine(path, _resourcesPath);
        }
    }
}
