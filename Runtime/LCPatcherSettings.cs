using System;
using System.IO;
using Nomnom.LCProjectPatcher.Attributes;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    [CreateAssetMenu(fileName = "NewLCPatcherSettings", menuName = "LC Project Patcher/LC Patcher Settings")]
    public class LCPatcherSettings: ScriptableObject {
        public AssetRipperSettings AssetRipperSettings => _assetRipperSettings;
        public GeneralSettings GeneralSettings => _generalSettings;

        [SerializedPath(nameof(GetBasePath))]
        [SerializeField] private string _baseUnityPath = "Unity";
        
        [SerializedPath(nameof(GetBasePath))]
        [SerializeField] private string _baseLethalCompanyPath = "LethalCompany";
        [SerializeField] private AssetRipperSettings _assetRipperSettings;
        [SerializeField] private GeneralSettings _generalSettings;

        public string GetBasePath(bool fullPath = false) {
            return GetFullPathOrNot("Assets", fullPath);
        }
        
        public string GetBaseUnityPath(bool fullPath = false) {
            return GetFullPathOrNot(Path.Combine(GetBasePath(), _baseUnityPath), fullPath);
        }
        
        public string GetBaseLethalCompanyPath(bool fullPath = false) {
            return GetFullPathOrNot(Path.Combine(GetBasePath(), _baseLethalCompanyPath), fullPath);
        }
        
        public string GetLethalCompanyGamePath(bool fullPath = false) {
            return GetFullPathOrNot(Path.Combine(GetBaseLethalCompanyPath(), _assetRipperSettings.BaseOutputPath), fullPath);
        }
        
        public string GetNativePath(bool fullPath = false) {
            return GetFullPathOrNot(_generalSettings.GetNativePath(GetBaseUnityPath()), fullPath);
        }
        
        public string GetAssetStorePath(bool fullPath = false) {
            return GetFullPathOrNot(_generalSettings.GetAssetStorePath(GetBaseUnityPath()), fullPath);
        }
        
        public string GetModsPath(bool fullPath = false) {
            return GetFullPathOrNot(_generalSettings.GetModsPath(GetBaseLethalCompanyPath()), fullPath);
        }
        
        public string GetToolsPath(bool fullPath = false) {
            return GetFullPathOrNot(_generalSettings.GetToolsPath(GetBaseLethalCompanyPath()), fullPath);
        }
        
        public string GetResourcesPath(bool fullPath = false) {
            return GetFullPathOrNot(_generalSettings.GetResourcesPath(GetBasePath()), fullPath);
        }
        
        public string GetStreamingAssetsPath(bool fullPath = false) {
            return GetFullPathOrNot(_generalSettings.GetStreamingAssetsPath(GetBasePath()), fullPath);
        }
        
        private string GetFullPathOrNot(string path, bool fullPath) {
            return fullPath ? Path.GetFullPath(path) : path;
        }
    }
}
