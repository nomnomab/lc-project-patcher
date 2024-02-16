using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomnom.LCProjectPatcher {
    [CreateAssetMenu(fileName = "NewLCPatcherRuntimeSettings", menuName = "LC Project Patcher/LC Patcher Runtime Settings")]
    public class LCPatcherRuntimeSettings: ScriptableObject {
        [Header("General")]
        public bool SkipIntro;
        public bool SkipMainMenu;
        public int SaveFileIndex;
        public bool SaveFileResetBeforeLoad;
        
        [Header("BepInEx")]
        public BepInExLocation BepInExLocation;
        public string CustomBepInExLocation;
        public bool LoadProjectPlugins = true;

        [Header("Cheats")] 
        public Object AutoLoadMoon;
        public string AutoLoadMoonSceneName;
        public bool InfiniteHealth;
        public bool InfiniteStamina;
        
        [Header("Experimental")]
        public bool LoadPosterizationShader;

        private void OnValidate() {
            if (!Application.isPlaying) return;
            HandleInfiniteHealth();
            SaveFileIndex = Mathf.Max(SaveFileIndex, 0);
        }

        public void HandleInfiniteHealth() {
            var startOfRound = GetStartOfRound();
            if (startOfRound == null) return;
            
            var allowLocalPlayerDeath = startOfRound.GetType().GetField("allowLocalPlayerDeath");
            allowLocalPlayerDeath.SetValue(startOfRound, !InfiniteHealth);
            Debug.Log($"Infinite health: {InfiniteHealth}");
        }

        public MonoBehaviour GetGameNetworkManager() {
            var gameNetworkManagerObj = GameObject.Find("NetworkManager");
            var gameNetworkManager = gameNetworkManagerObj.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == "GameNetworkManager");
            return (MonoBehaviour)gameNetworkManager;
        }
        
        public MonoBehaviour GetStartOfRound() {
            return GameObject
                .FindObjectsOfType<MonoBehaviour>()
                .FirstOrDefault(x => x.GetType().Name == "StartOfRound");
        }

        public MonoBehaviour GetTerminal() {
            return GameObject
                .FindObjectsOfType<MonoBehaviour>()
                .FirstOrDefault(x => x.GetType().Name == "Terminal");
        }
    }
}
