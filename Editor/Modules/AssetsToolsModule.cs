using System.IO;
using AssetsTools.NET.Extra;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class AssetsToolsModule {
        public static void GetShader(LCPatcherSettings settings) {
            AssetsManager assetsManager = new();
            
            // Load Unity type tree file so we can actually use our game's asset files
            var classPackagePath = Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/AssetsTools.NET/uncompressed.tpk");
            assetsManager.LoadClassPackage(classPackagePath);
            
            assetsManager.UnloadAll();
        }
    }
}