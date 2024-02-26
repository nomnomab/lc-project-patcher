using System;
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
        public bool InfiniteHealth;
        public bool InfiniteStamina;
        public bool SkipTerminalIntro;
        public int StartingCredits = -1;
        public float Time;
        
        [Header("Moons")]
        public bool AutoLoadMoon;
        public Object AutoLoadMoonReference;
        public string AutoLoadMoonSceneName;
        
        [Header("Experimental")]
        public bool LoadPosterizationShader;

        [Header("Other")] 
        public bool DisableAutomaticScriptableObjectReloading;
        public bool DisablePreInitScriptCoroutineReplacer;

        private static MonoBehaviour _timeOfDay;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnLoad() {
            _timeOfDay = null;
        }

        private void OnValidate() {
            if (!Application.isPlaying) return;
            HandleInfiniteHealth();
            HandleTimeOfDay();
            SaveFileIndex = Mathf.Max(SaveFileIndex, 0);
        }

        public void HandleInfiniteHealth() {
            var startOfRound = GetStartOfRound();
            if (startOfRound == null) return;
            
            var allowLocalPlayerDeath = startOfRound.GetType().GetField("allowLocalPlayerDeath");
            allowLocalPlayerDeath.SetValue(startOfRound, !InfiniteHealth);
            Debug.Log($"Infinite health: {InfiniteHealth}");
        }

        public void HandleTimeOfDay() {
            var timeOfDay = GetTimeOfDay();
            if (!timeOfDay) return;

            var currentDayTime = GetCurrentDayTimeField();
            var totalTime = GetTotalTimeField();
            var timeNormalized = Time;
            var newTime = timeNormalized * (float)totalTime?.GetValue(timeOfDay);
            var globalTime = GetGlobalTimeField();
            var startingGlobalTime = GetConstantStartingGlobalTimeField();
            
            var newGlobalTime = (int)startingGlobalTime?.GetValue(timeOfDay) + newTime;
            Debug.Log($"{globalTime.GetValue(timeOfDay)} to {newGlobalTime}");
            globalTime.SetValue(timeOfDay, newGlobalTime);
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

        public MonoBehaviour GetTimeOfDay() {
            if (!Application.isPlaying) {
                return null;
            }
            
            if (_timeOfDay) {
                return _timeOfDay;
            }

            var timeOfDay = GameObject
                .FindObjectsOfType<MonoBehaviour>()
                .FirstOrDefault(x => x.GetType().Name == "TimeOfDay");

            if (Application.isPlaying) {
                _timeOfDay = timeOfDay;
            }

            return _timeOfDay;
        }

        public FieldInfo GetCurrentDayTimeField() {
            var timeOfDay = GetTimeOfDay();
            if (!timeOfDay) return null;
            
            return timeOfDay.GetType().GetField("currentDayTime");
        }
        
        public FieldInfo GetTotalTimeField() {
            var timeOfDay = GetTimeOfDay();
            if (!timeOfDay) return null;
            
            return timeOfDay.GetType().GetField("totalTime");
        }
        
        public FieldInfo GetGlobalTimeField() {
            var timeOfDay = GetTimeOfDay();
            if (!timeOfDay) return null;
            
            return timeOfDay.GetType().GetField("globalTime");
        }
        
        public FieldInfo GetConstantStartingGlobalTimeField() {
            var timeOfDay = GetTimeOfDay();
            if (!timeOfDay) return null;
            
            return timeOfDay.GetType().GetField("startingGlobalTime");
        }
    }
}
