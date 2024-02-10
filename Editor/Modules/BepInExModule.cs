using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class BepInExModule {
        private readonly static string BepInExHarmonyDllMeta = @"
fileFormatVersion: 2
guid: 70a99d8d3ad15054da80f101339c7867
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Editor: 1
        Exclude Linux64: 1
        Exclude OSXUniversal: 1
        Exclude Win: 1
        Exclude Win64: 1
  - first:
      Any: 
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS
  - first:
      Standalone: Linux64
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Standalone: OSXUniversal
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Standalone: Win
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Standalone: Win64
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";

        public static UniTask Install(LCPatcherSettings settings) {
            var pluginsPath = Path.Combine(settings.GetToolsPath(), "Plugins");
            var packageBepInExPath = Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/BepInEx~");
            var bepInExPath = Path.Combine(pluginsPath, "BepInEx");
            ModuleUtility.CreateDirectory(bepInExPath);
            
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var bepInExRootFolder = Path.Combine(projectRoot, "Lethal Company");
            var bepInExInnerFolder = Path.Combine(bepInExRootFolder, "BepInEx");
            var bepInExCoreFolder = Path.Combine(bepInExInnerFolder, "core");
            
            // copy over bepinex dlls
            var bepInExFiles = Directory.GetFiles(packageBepInExPath, "*.*");
            for (var i = 0; i < bepInExFiles.Length; i++) {
                var file = bepInExFiles[i];
                EditorUtility.DisplayProgressBar("Installing BepInEx", $"Installing {Path.GetFileName(file)}", (float)i / bepInExFiles.Length);
                try {
                    File.Copy(file, Path.Combine(bepInExPath, Path.GetFileName(file)), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
                
                try {
                    File.Copy(file, Path.Combine(bepInExCoreFolder, Path.GetFileName(file)), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }

            // create meta for BepInEx.Harmony.dll
            var harmonyDllPath = Path.Combine(bepInExPath, "BepInEx.Harmony.dll.meta");
            try {
                File.WriteAllText(harmonyDllPath, BepInExHarmonyDllMeta);
            } catch (Exception e) {
                Debug.LogError(e);
            }
            
            return UniTask.CompletedTask;
        }

        public static void InstallMonoMod(LCPatcherSettings settings) {
            var monoModInPackagePath = Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/MonoMod~");
            var dlls = Directory.GetFiles(monoModInPackagePath, "*.dll");
            var bepInExPath = Path.Combine(settings.GetToolsPath(), "Plugins", "MonoMod");
            
            ModuleUtility.CreateDirectory(bepInExPath);
            
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var bepInExRootFolder = Path.Combine(projectRoot, "Lethal Company");
            var bepInExInnerFolder = Path.Combine(bepInExRootFolder, "BepInEx");
            var bepInExCoreFolder = Path.Combine(bepInExInnerFolder, "core");
            
            for (var i = 0; i < dlls.Length; i++) {
                var dll = dlls[i];
                EditorUtility.DisplayProgressBar("Installing MonoMod", $"Installing {Path.GetFileName(dll)}", (float)i / dlls.Length);
                try {
                    File.Copy(dll, Path.Combine(bepInExPath, Path.GetFileName(dll)), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
                
                try {
                    File.Copy(dll, Path.Combine(bepInExCoreFolder, Path.GetFileName(dll)), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }
            
            EditorUtility.ClearProgressBar();
        }

        public static void CopyUtility(LCPatcherSettings settings) {
            var pluginsPath = Path.Combine(settings.GetToolsPath(), "Plugins");
            var bepInExPath = Path.Combine(pluginsPath, "BepInEx");
            var utilityPath = Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/BepInExUtility~");
            var files = Directory.GetFiles(utilityPath);

            var utilityFolder = Path.Combine(bepInExPath, "Utility");
            ModuleUtility.CreateDirectory(utilityFolder);
            
            for (var i = 0; i < files.Length; i++) {
                var file = files[i];
                EditorUtility.DisplayProgressBar("Installing BepInEx Utility", $"Installing {Path.GetFileName(file)}", (float)i / files.Length);
                try {
                    File.Copy(file, Path.Combine(utilityFolder, $"{Path.GetFileNameWithoutExtension(file)}.cs"), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }
        }

        public static void CopyTemplateFolder() {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var bepInExRootFolder = Path.Combine(projectRoot, "Lethal Company");
            
            var bepInExInnerFolder = Path.Combine(bepInExRootFolder, "BepInEx");
            var bepInExConfigFolder = Path.Combine(bepInExInnerFolder, "config");
            var bepInExCoreFolder = Path.Combine(bepInExInnerFolder, "core");
            var bepInExPluginsFolder = Path.Combine(bepInExInnerFolder, "plugins");
            var bepInExPatcherFolder = Path.Combine(bepInExInnerFolder, "patchers");
            
            var dataFolder = Path.Combine(bepInExRootFolder, "Lethal Company_Data");
            var managedFolder = Path.Combine(dataFolder, "Managed");
            var exeNotPath = Path.Combine(bepInExRootFolder, "Lethal Company.exenot");
            
            Directory.CreateDirectory(bepInExRootFolder);
            Directory.CreateDirectory(bepInExInnerFolder);
            Directory.CreateDirectory(bepInExConfigFolder);
            Directory.CreateDirectory(bepInExCoreFolder);
            Directory.CreateDirectory(bepInExPluginsFolder);
            Directory.CreateDirectory(bepInExPatcherFolder);
            
            Directory.CreateDirectory(dataFolder);
            Directory.CreateDirectory(managedFolder);

            if (!File.Exists(exeNotPath)) {
                File.Create(exeNotPath).Close();
            }
        }
    }
}
