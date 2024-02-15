using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher {
    [CreateAssetMenu(fileName = "NewLCPatcherRuntimeSettings", menuName = "LC Project Patcher/LC Patcher Runtime Settings")]
    public class LCPatcherRuntimeSettings: ScriptableObject {
        [Header("General")]
        public bool SkipMainMenu;
        public BepInExLocation BepInExLocation;
        public string CustomBepInExLocation;
        public bool LoadProjectPlugins = true;

        [Header("Cheats")] 
        public Object AutoLoadMoon;
        public bool InfiniteHealth;
        public bool InfiniteStamina;
        
        [Header("Experimental")]
        public bool LoadPosterizationShader;

        private void OnValidate() {
            if (!Application.isPlaying) return;
            HandleInfiniteHealth();
        }

        public void HandleInfiniteHealth() {
            var startOfRound = FindObjectsOfType<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == "StartOfRound");
            if (startOfRound == null) {
                Debug.LogWarning("StartOfRound not found!");
                return;
            }
            
            var allowLocalPlayerDeath = startOfRound.GetType().GetField("allowLocalPlayerDeath");
            allowLocalPlayerDeath.SetValue(startOfRound, !InfiniteHealth);
            Debug.Log($"Infinite health: {InfiniteHealth}");
        }
    }
}
