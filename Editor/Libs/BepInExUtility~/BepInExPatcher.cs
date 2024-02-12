#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class BepInExPatcher: MonoBehaviour {
    private static string LethalCompanyDataFolder {
        get {
            var path = EditorPrefs.GetString("nomnom.lc_project_patcher.lc_data_folder");
            if (string.IsNullOrEmpty(path)) {
                return null;
            }
                
            if (!path.EndsWith("_Data")) {
                var folderName = Path.GetFileNameWithoutExtension(path);
                var dataFolder = $"{folderName}_Data";
                path = Path.Combine(path, dataFolder);

                if (!Directory.Exists(path)) {
                    Debug.LogError("The data path needs to end in \"_Data\"!");
                    return null;
                }
            }

            return path;
        }
    }
    
    private static bool WantsInstantStart => EditorPrefs.GetBool("nomnom.lc_project_patcher.skip_main_menu", false);
    
    private static string ActualExePath => Path.Combine(LethalCompanyDataFolder, "..", "Lethal Company.exe");
    private static string FakeExePath => Path.Combine(Application.dataPath, "..", "Lethal Company", "Lethal Company.exenot");
    private static string DirectoryPath => Path.GetDirectoryName(ActualExePath);
    private static string GameDataPath => Path.Combine(DirectoryPath, "Lethal Company_Data");
    private static string ManagedPath => Path.Combine(GameDataPath, "Managed");
    
    private static List<Assembly> _assemblies = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnLoad() {
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        ResetNetcodeRpcTables.ResetRpcFuncTable();
        
        // reset event system m_EventSystems
        // var eventSystems = typeof(EventSystem).GetField("m_EventSystems", BindingFlags.NonPublic | BindingFlags.Static);
        // var eventSystemsValue = (List<EventSystem>)eventSystems.GetValue(null);
        // foreach (var eventSystem in eventSystemsValue) {
        //     Debug.Log($"There is {eventSystem}", eventSystem);
        // }
        // eventSystemsValue.Clear();

        var useGameBepInEx = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_game_bepinex", false);
        var filePath = useGameBepInEx ? ActualExePath : FakeExePath;
        var setExecutablePath = typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.NonPublic | BindingFlags.Static);
        setExecutablePath.Invoke(null, new object[] { filePath, null, null, null });

        // reset logger sources
        var sources = typeof(BepInEx.Logging.Logger).GetProperty("Sources", BindingFlags.Public | BindingFlags.Static);
        var sourcesValue = (ICollection<ILogSource>)sources.GetValue(null);
        sourcesValue.Clear();
        
        // reset _Listeners
        var listeners = typeof(BepInEx.Logging.Logger).GetField("_Listeners", BindingFlags.NonPublic | BindingFlags.Static);
        var listenersValue = (ICollection<ILogListener>)listeners.GetValue(null);
        listenersValue.Clear();
        
        // reset Chainloader._initialized
        var initialized = typeof(Chainloader).GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);
        initialized.SetValue(null, false);
        
        // reset _loaded
        var loaded = typeof(Chainloader).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
        loaded.SetValue(null, false);
        
        // reset internalLogsInitialized
        var internalLogsInitialized = typeof(BepInEx.Logging.Logger).GetField("internalLogsInitialized", BindingFlags.NonPublic | BindingFlags.Static);
        internalLogsInitialized.SetValue(null, false);
        
        // change UnityLogListener.WriteStringToUnityLog to debug log
        var writeStringToUnityLog = typeof(BepInEx.Logging.UnityLogListener).GetField("WriteStringToUnityLog", BindingFlags.NonPublic | BindingFlags.Static);
        writeStringToUnityLog.SetValue(null, new Action<string>(Debug.Log));
        
        var harmony = new Harmony("com.nomnom.test-bepinex");
        harmony.PatchAll(typeof(FindPluginTypesPatch));
        harmony.PatchAll(typeof(OverrideEditorCheck));
        
        // let up do something dumb! :)
        // get all dlls from game's plugins and manually load them since
        // sub-dependencies are not always loaded
        var gamePlugins = Path.Combine(Path.GetDirectoryName(filePath), "BepInEx", "plugins");
        var gameDlls = Directory.GetFiles(gamePlugins, "*.dll", SearchOption.AllDirectories);
        _assemblies.Clear();

        foreach (var gameDll in gameDlls) {
            Debug.Log($"Loading {Path.GetFileName(gameDll)}");
            try {
                _assemblies.Add(Assembly.LoadFile(gameDll));
            } catch (Exception e) {
                Debug.LogError($"Failed to load {gameDll}: {e}");
            }
        }
        
        Chainloader.Initialize(filePath, false);
        
        if (!useGameBepInEx) {
            OverridePaths();
        } else {
            Debug.Log("Using the normal game's BepInEx folder");
        }
        // DebugPaths();

        try {
            harmony.PatchAll(typeof(IgnoreILHelpers));
        } catch (Exception e) {
            Debug.LogWarning($"Failed to patch IL helpers: {e}");
        }
        
        Chainloader.Start();
        harmony.UnpatchSelf();
        harmony.PatchAll(typeof(OnlineDisabler));
        harmony.PatchAll(typeof(SkipMenu));
        harmony.PatchAll(typeof(EventSystemPatch));
        
        Debug.Log("Loaded BepInEx!");
        
        var obj = new GameObject("CustomPlugin");
        obj.AddComponent<BepInExPatcher>();
        DontDestroyOnLoad(obj);
        
        Debug.Log($"BepInExPatcher took {stopwatch.ElapsedMilliseconds}ms to load");
    }

    private static void OverridePaths() {
        Debug.Log($"Using local BepInEx");
        
        // ExecutablePath
        var executablePathProperty = typeof(Paths).GetProperty("ExecutablePath");
        executablePathProperty.SetValue(null, ActualExePath);
        
        // ManagedPath
        var managedPathProperty = typeof(Paths).GetProperty("ManagedPath");
        managedPathProperty.SetValue(null, ManagedPath);
        
        // DllSearchPaths
        var dllSearchPathsProperty = typeof(Paths).GetProperty("DllSearchPaths");
        dllSearchPathsProperty.SetValue(null, new string[] {ManagedPath});
    }

    private static void DebugPaths() {
        Debug.Log($"BepInExAssemblyDirectory: {Paths.BepInExAssemblyDirectory}");
        Debug.Log($"BepInExAssemblyPath: {Paths.BepInExAssemblyPath}");
        Debug.Log($"BepInExRootPath: {Paths.BepInExRootPath}");
        Debug.Log($"ExecutablePath: {Paths.ExecutablePath}");
        Debug.Log($"GameRootPath: {Paths.GameRootPath}");
        Debug.Log($"ManagedPath: {Paths.ManagedPath}");
        Debug.Log($"ConfigPath: {Paths.ConfigPath}");
        Debug.Log($"BepInExConfigPath: {Paths.BepInExConfigPath}");
        Debug.Log($"CachePath: {Paths.CachePath}");
        Debug.Log($"PatcherPluginPath: {Paths.PatcherPluginPath}");
        Debug.Log($"PluginPath: {Paths.PluginPath}");
        Debug.Log($"ProcessName: {Paths.ProcessName}");
    }

    private void OnDestroy() {
        Harmony.UnpatchAll();
        ResetNetcodeRpcTables.DidReset = false;

        DisposePlugins();
        _assemblies.Clear();

        // clean up hidden objects
        foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>()) {
            if (EditorUtility.IsPersistent(obj.transform.root.gameObject)) {
                continue;
            }

            if (obj.name.StartsWith("Scene") || obj.name == "Default Volume") {
                continue;
            }

            if (obj.hideFlags == HideFlags.HideAndDontSave) {
                Debug.Log($"Destroying {obj}");
                Destroy(obj);
            }
        }
    }

    private static void DisposePlugins() {
        // clear all plugin static information
        var types = _assemblies.SelectMany(a => a.GetTypes());
        foreach (var type in types) {
            var fields = type
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => !x.Name.StartsWith("<"));
            foreach (var field in fields) {
                try {
                    if (field.FieldType.IsValueType) {
                        field.SetValue(null, Activator.CreateInstance(field.FieldType));
                        Debug.Log($"Reset value type \"{field.Name}\" in {type.FullName}");
                    } else {
                        if (typeof(IList).IsAssignableFrom(field.FieldType)) {
                            var list = (IList)field.GetValue(null);
                            list?.Clear();
                            // field.SetValue(null, null);
                            Debug.Log($"Reset list \"{field.Name}\" in {type.FullName}");
                            continue;
                        }
                        
                        field.SetValue(null, null);
                        Debug.Log($"Reset reference type \"{field.Name}\" in {type.FullName}");
                    }
                } catch {
                    // Debug.LogWarning($"Failed to reset {field.Name} in {type.FullName}: {e}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Chainloader))]
    private static class OverrideEditorCheck {
        [HarmonyPatch("IsEditor", MethodType.Getter)]
        [HarmonyPostfix]
        private static void Get(ref bool __result) {
            __result = false;
        }
    }
    
    [HarmonyPatch]
    private static class FindPluginTypesPatch {
        public static MethodBase TargetMethod() {
            return AccessTools
                .Method(typeof(TypeLoader), nameof(TypeLoader.FindPluginTypes))
                .MakeGenericMethod(typeof(PluginInfo));
        }

        public static Dictionary<string, List<PluginInfo>> Postfix(Dictionary<string, List<PluginInfo>> result) {
            var file = typeof(FindPluginTypesPatch).Module.FullyQualifiedName;
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(file, TypeLoader.ReaderParameters);
            var hasBepinPluginsFunction = AccessTools.Method(typeof(Chainloader), "HasBepinPlugins");

            if (!(bool)hasBepinPluginsFunction.Invoke(null, new object[] { assemblyDefinition })) {
                result[file] = new List<PluginInfo>();
                assemblyDefinition.Dispose();
                Debug.Log("No BepInEx plugins found in Assembly-CSharp!");
            } else {
                var list = assemblyDefinition.MainModule.Types
                    .Select(Chainloader.ToPluginInfo)
                    .Where(t => t != null)
                    .ToList();
                result[file] = list;
                assemblyDefinition.Dispose();
                Debug.Log($"Found {list.Count} BepInEx plugins in Assembly-CSharp!");
            }

            return result;
        }
    }

    [HarmonyPatch]
    private static class IgnoreILHelpers {
        public static IEnumerable<MethodBase> TargetMethods() {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                Type[] types;
                try {
                    types = assembly.GetTypes();
                } catch {
                    Debug.LogError($"Failed to get types from {assembly.FullName}");
                    continue;
                }
                
                foreach (var type in types) {
                    if (type == null) continue;

                    var isPlugin = false;
                    try {
                        isPlugin = typeof(BaseUnityPlugin).IsAssignableFrom(type);
                    } catch {
                        Debug.LogError($"Failed to check if {type.FullName} is a BaseUnityPlugin");
                        continue;
                    }
                    
                    if (isPlugin) {
                        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
                        foreach (var method in methods) {
                            if (method.Name == "IlHook") {
                                yield return (MethodBase)method;
                            }
                        }
                    }
                }
            }
        }

        public static bool Prefix() {
            return false;
        }
    }

    [HarmonyPatch(typeof(PreInitSceneScript))]
    private static class OnlineDisabler {
        [HarmonyPatch(nameof(PreInitSceneScript.ChooseLaunchOption))]
        [HarmonyPrefix]
        private static bool ChooseLaunchOption(bool online) {
            if (online) {
                EditorUtility.DisplayDialog("Error", "Online mode is not supported!", "OK");
                return false;
            }
    
            return true;
        }
        
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void InstantlyStartGame() {
            if (!WantsInstantStart) return;
            FindObjectOfType<BepInExPatcher>().StartCoroutine(Waiter());
        }
        
        private static IEnumerator Waiter() {
            yield return new WaitForSeconds(0.5f + 0.2f);
            SceneManager.LoadScene("InitSceneLANMode");
            yield return new WaitForSeconds(0.2f);
            SceneManager.LoadScene("MainMenu");
        }
    }
    
    [HarmonyPatch(typeof(MenuManager))]
    private static class SkipMenu {
        [HarmonyPatch("Start")]
        [HarmonyPrefix]
        private static void Start(MenuManager __instance) {
            if (!WantsInstantStart) return;
            __instance.StartCoroutine(Waiter());
        }
    
        private static IEnumerator Waiter() {
            yield return new WaitForSeconds(0.1f);
            FindObjectOfType<MenuManager>().ClickHostButton();
            yield return new WaitForSeconds(0.1f);
            FindObjectOfType<MenuManager>().ConfirmHostButton();
        }
    }
    
    [HarmonyPatch(typeof(EventSystem))]
    private static class EventSystemPatch {
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        private static void RemoveDuplicateEventSystems(ref List<EventSystem> ___m_EventSystems) {
            if (!Application.isPlaying) {
                return;
            }
    
            if (___m_EventSystems.Count == 1) return;
            for (var i = 1; i < ___m_EventSystems.Count; i++) {
                var eventSystem = ___m_EventSystems[i];
                eventSystem.enabled = false;
                Debug.Log($"Removed duplicate EventSystem: at {i} where length is {___m_EventSystems.Count}");
                ___m_EventSystems.Remove(eventSystem);
            }
        }
    }
}
#endif
