using System.IO;

namespace Nomnom.LCProjectPatcher.Modules {
    public static class ModuleUtility {
        public static void CopyFilesRecursively(string sourcePath, string targetPath) {
            Directory.CreateDirectory(targetPath);
            
            foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }
            
            foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)) {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), overwrite: true);
            }
        }
    }
}
