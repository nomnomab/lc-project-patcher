using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor.Modules {
    public static class SteamGameModule {
        private readonly static string[] DllsToCopy = new[] {
            "AmazingAssets.TerrainToMesh.dll",
            "ClientNetworkTransform.dll",
            "DissonanceVoip.dll",
            "Facepunch Transport for Netcode for GameObjects.dll",
            "Facepunch.Steamworks.Win64.dll",
            "Newtonsoft.Json.dll",
            // "Assembly-CSharp.dll", // experimental
            "Assembly-CSharp-firstpass.dll",
        };
        
        private readonly static string[] SpecialDllsToCopy = new[] {
            "AudioPluginDissonance.dll",
            "discord_game_sdk.dll",
            "opus.dll",
            "phonon_fmod.dll",
            // "steam_api64.dll",
        };
        
        private readonly static string[] SpecialDllsToCopyIntoHidden = new[] {
            "steam_api64.dll",
        };
        
        public static void CopyManagedDlls(LCPatcherSettings settings) {
            var lcDataFolder = ModuleUtility.LethalCompanyDataFolder;
            var gameManagedFolder = Path.Combine(lcDataFolder, "Managed");
            var projectPluginsFolder = Path.Combine(settings.GetLethalCompanyGamePath(), "Plugins");
            
            Directory.CreateDirectory(projectPluginsFolder);
            
            for (var i = 0; i < DllsToCopy.Length; i++) {
                var dll = DllsToCopy[i];
                var gamePath = Path.Combine(gameManagedFolder, dll);
                var projectPath = Path.Combine(projectPluginsFolder, dll);

                EditorUtility.DisplayProgressBar("Copying game dlls", $"Copying {dll} to {projectPath}", (float)i / DllsToCopy.Length);

                if (!File.Exists(gamePath)) {
                    Debug.LogWarning($"Game dll \"{gamePath}\" does not exist");
                    continue;
                }

                File.Copy(gamePath, projectPath, overwrite: true);
            }
            
            EditorUtility.ClearProgressBar();
        }

        public static void CopyPluginDlls(LCPatcherSettings settings) {
            var lcDataFolder = ModuleUtility.LethalCompanyDataFolder;
            var gameSpecialPluginsFolder = Path.Combine(lcDataFolder, "Managed", "Plugins", "x86_64");
            
            var projectPluginsFolder = Path.Combine(settings.GetLethalCompanyGamePath(), "Plugins");
            var projectSpecialPluginsFolder = Path.Combine(projectPluginsFolder, "x86_64");
            var projectSpecialPluginsFolderHidden = Path.Combine(projectSpecialPluginsFolder, "Hidden~");
            
            Directory.CreateDirectory(projectSpecialPluginsFolder);
            Directory.CreateDirectory(projectSpecialPluginsFolderHidden);
            
            for (var i = 0; i < SpecialDllsToCopy.Length; i++) {
                var dll = SpecialDllsToCopy[i];
                var gamePath = Path.Combine(gameSpecialPluginsFolder, dll);
                var projectPath = Path.Combine(projectSpecialPluginsFolder, dll);

                EditorUtility.DisplayProgressBar("Copying game dlls", $"Copying {dll} to {projectPath}", (float)i / SpecialDllsToCopy.Length);

                if (!File.Exists(gamePath)) {
                    Debug.LogWarning($"Game dll \"{gamePath}\" does not exist");
                    continue;
                }

                File.Copy(gamePath, projectPath, overwrite: true);
            }
            
            for (var i = 0; i < SpecialDllsToCopyIntoHidden.Length; i++) {
                var dll = SpecialDllsToCopyIntoHidden[i];
                var gamePath = Path.Combine(gameSpecialPluginsFolder, dll);
                var projectPath = Path.Combine(projectSpecialPluginsFolderHidden, dll);

                EditorUtility.DisplayProgressBar("Copying game dlls", $"Copying {dll} to {projectPath}", (float)i / SpecialDllsToCopyIntoHidden.Length);

                if (!File.Exists(gamePath)) {
                    Debug.LogWarning($"Game dll \"{gamePath}\" does not exist");
                    continue;
                }

                File.Copy(gamePath, projectPath, overwrite: true);
            }
            
            EditorUtility.ClearProgressBar();
        }
    }
}
