using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Modules {
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
        
        public static UniTask Patch() {
            var lcDataFolder = ModuleUtility.GetLethalCompanyDataFolder();
            
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Plugins"));
            for (var i = 0; i < DllsToCopy.Length; i++) {
                var dll = DllsToCopy[i];
                var gamePath = Path.Combine(lcDataFolder, "Managed", dll);
                var projectPath = Path.Combine(Application.dataPath, "Plugins", dll);

                EditorUtility.DisplayProgressBar("Copying game dlls", $"Copying {dll} to {projectPath}", (float)i / DllsToCopy.Length);

                if (!File.Exists(gamePath)) {
                    Debug.LogWarning($"Game dll \"{gamePath}\" does not exist");
                    continue;
                }

                File.Copy(gamePath, projectPath, overwrite: true);
            }
            
            EditorUtility.ClearProgressBar();
            
            Debug.Log("Game dlls copied");

            return UniTask.CompletedTask;
        }
    }
}
