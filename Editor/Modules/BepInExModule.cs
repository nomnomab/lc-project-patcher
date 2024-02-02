using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class BepInExModule {
        private readonly static string[] BepInExDlls = {
            "0Harmony.dll",
            "BepInEx.dll",
            "BepInEx.Harmony.dll",
            "BepInEx.Preloader.dll",
            "HarmonyXInterop.dll",
            "Mono.Cecil.dll",
            "Mono.Cecil.Mdb.dll",
            "Mono.Cecil.Pdb.dll",
            "Mono.Cecil.Rocks.dll",
        };

        private readonly static string[] MonoModDlls = {
            "MonoMod.DebugIL",
            "MonoMod",
            "MonoMod.RuntimeDetour",
            "MonoMod.RuntimeDetour.HookGen",
            "MonoMod.Utils",
        };

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
            var githubLink = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip";
            var pluginsPath = Path.Combine(settings.GetToolsPath(), "Plugins");
            var tempBepInExPath = Path.Combine(pluginsPath, "BepInEx~");
            var bepInExPath = Path.Combine(pluginsPath, "BepInEx");
            var bepInExZipPath = Path.Combine(pluginsPath, "BepInEx.zip");

            try {
                if (Directory.Exists(tempBepInExPath)) {
                    Directory.Delete(tempBepInExPath, true);
                }
            } catch (Exception e) {
                Debug.LogError(e);
                return UniTask.CompletedTask;
            }

            ModuleUtility.CreateDirectory(tempBepInExPath);
            ModuleUtility.CreateDirectory(bepInExPath);

            EditorUtility.DisplayProgressBar("Downloading BepInEx", "Downloading BepInEx", 0);
            try {
                using (var client = new WebClient()) {
                    client.DownloadProgressChanged += (sender, args) => {
                        EditorUtility.DisplayProgressBar("Downloading BepInEx", "Downloading BepInEx", args.ProgressPercentage / 100f);
                    };
                    client.DownloadFileAsync(new Uri(githubLink), bepInExZipPath);
                    while (client.IsBusy) { }
                    EditorUtility.ClearProgressBar();
                }

                ZipFile.ExtractToDirectory(bepInExZipPath, tempBepInExPath);
            } catch (Exception e) {
                Debug.LogError(e);
                try {
                    Directory.Delete(tempBepInExPath);
                } catch (Exception e2) {
                    Debug.LogError(e2);
                }
                return UniTask.CompletedTask;
            }
            finally {
                EditorUtility.ClearProgressBar();
                if (File.Exists(bepInExZipPath)) {
                    File.Delete(bepInExZipPath);
                }
            }

            // zip/BepInEx/core/*.dll
            var dllPath = Path.Combine(tempBepInExPath, "BepInEx", "core");
            var dlls = Directory.GetFiles(dllPath, "*.dll")
                .Where(x => BepInExDlls.Contains(Path.GetFileName(x)))
                .ToArray();
            
            EditorUtility.DisplayProgressBar("Installing BepInEx", "Installing BepInEx", 0);
            for (var i = 0; i < dlls.Length; i++) {
                var dll = dlls[i];
                EditorUtility.DisplayProgressBar("Installing BepInEx", $"Installing {Path.GetFileName(dll)}", (float)i / dlls.Length);
                try {
                    File.Copy(dll, Path.Combine(bepInExPath, Path.GetFileName(dll)), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }
            EditorUtility.ClearProgressBar();

            // create meta for BepInEx.Harmony.dll
            var harmonyDllPath = Path.Combine(bepInExPath, "BepInEx.Harmony.dll.meta");
            try {
                File.WriteAllText(harmonyDllPath, BepInExHarmonyDllMeta);
            } catch (Exception e) {
                Debug.LogError(e);
            }
            
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var bepInExRootFolder = Path.Combine(projectRoot, "Lethal Company");
            var bepInExInnerFolder = Path.Combine(bepInExRootFolder, "BepInEx");
            var bepInExCoreFolder = Path.Combine(bepInExInnerFolder, "core");
            
            foreach (var dll in Directory.GetFiles(dllPath, "*.dll")) {
                try {
                    File.Copy(dll, Path.Combine(bepInExCoreFolder, Path.GetFileName(dll)), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }

            try {
                Directory.Delete(tempBepInExPath, true);
            } catch (Exception e) {
                Debug.LogError(e);
            }
            
            return UniTask.CompletedTask;
        }

        public static void InstallMonoMod(LCPatcherSettings settings) {
            var monoModInPackagePath = Path.GetFullPath("Packages/com.nomnom.lc-project-patcher/Editor/Libs/MonoMod~");
            var dlls = Directory.GetFiles(monoModInPackagePath, "*.dll")
                .Where(x => MonoModDlls.Contains(Path.GetFileNameWithoutExtension(x)))
                .ToArray();
            var bepInExPath = Path.Combine(settings.GetToolsPath(), "Plugins", "MonoMod");
            
            ModuleUtility.CreateDirectory(bepInExPath);
            
            for (var i = 0; i < dlls.Length; i++) {
                var dll = dlls[i];
                EditorUtility.DisplayProgressBar("Installing MonoMod", $"Installing {Path.GetFileName(dll)}", (float)i / dlls.Length);
                try {
                    File.Copy(dll, Path.Combine(bepInExPath, Path.GetFileName(dll)), true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }
            
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var bepInExRootFolder = Path.Combine(projectRoot, "Lethal Company");
            var bepInExInnerFolder = Path.Combine(bepInExRootFolder, "BepInEx");
            var bepInExCoreFolder = Path.Combine(bepInExInnerFolder, "core");

            foreach (var dll in Directory.GetFiles(monoModInPackagePath, "*.dll")) {
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
