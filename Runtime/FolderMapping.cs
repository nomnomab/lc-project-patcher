using System;
using Nomnom.LCProjectPatcher.Attributes;

namespace Nomnom.LCProjectPatcher {
    [Serializable]
    public struct FolderMapping {
        public string folderPath;
        [SerializedPath(nameof(LCPatcherSettings.GetLethalCompanyGamePath))]
        public string outputPath;
            
        public FolderMapping(string folderPath) {
            this.folderPath = folderPath;
            this.outputPath = folderPath;
        }
            
        public FolderMapping(string folderPath, string outputPath) {
            this.folderPath = folderPath;
            this.outputPath = outputPath;
        }
    }
}
