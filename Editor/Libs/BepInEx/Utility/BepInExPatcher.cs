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
using Mono.Cecil.Cil;
using Nomnom.LCProjectPatcher;
using Nomnom.LCProjectPatcher.Editor.Modules;
using Patches;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class BepInExPatcher: MonoBehaviour {
    private static bool LoadPosterizationShader => ModuleUtility.GetPatcherRuntimeSettings().LoadPosterizationShader;
    private static BepInExLocation BepInExLocation => ModuleUtility.GetPatcherRuntimeSettings().BepInExLocation;
    private static string CustomBepInExLocation => ModuleUtility.GetPatcherRuntimeSettings().CustomBepInExLocation;
    private static bool LoadProjectPlugins => ModuleUtility.GetPatcherRuntimeSettings().LoadProjectPlugins;
    
    private static CustomPassVolume _posterizationVolume;
    private static List<Assembly> _assemblies = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnLoad() {
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        
        ResetNetcodeRpcTablesPatch.ResetRpcFuncTable();
        ModuleUtility.ResetInstance();

        var bepInExFolder = ModuleUtility.BepInExFolder;
        var gameExePath = ModuleUtility.GameExePath;
        var gamePlugins = ModuleUtility.GamePluginsPath;
        Debug.Log($"Using BepInEx at \"{bepInExFolder}\"");

        // var doorStopInvokeDllPath = Path.GetFullPath(Path.Combine(bepInExFolder, "core", "BepInEx.Preloader.dll"));
        // var doorStopManagedFolderPath = Path.GetFullPath(ManagedPath);
        // var doorStopProcessPath = Path.GetFullPath(gameExePath);
        // var allPluginRecursiveFolders = new List<string>();
        // foreach (var plugin in Directory.GetDirectories(gamePlugins)) {
        //     allPluginRecursiveFolders.Add(plugin);
        // }
        // var doorStopDllSearchDirs = string.Join(Path.PathSeparator, new string[] {
        //     Path.GetFullPath(ManagedPath),
        //     // @"C:\Users\nomno\Documents\Modding\LethalCompany_v49\LethalCompany_PatcherTest\Library\ScriptAssemblies"
        //     Path.GetFullPath(gamePlugins),
        // }.Concat(allPluginRecursiveFolders).ToArray());
        //
        // Debug.Log($"Setting DOORSTOP_INVOKE_DLL_PATH to \"{doorStopInvokeDllPath}\"");
        // Debug.Log($"Setting DOORSTOP_MANAGED_FOLDER_DIR to \"{doorStopManagedFolderPath}\"");
        // Debug.Log($"Setting DOORSTOP_PROCESS_PATH to \"{doorStopProcessPath}\"");
        // Debug.Log($"Setting DOORSTOP_DLL_SEARCH_DIRS to \"{doorStopDllSearchDirs}\"");
        //
        // Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", doorStopInvokeDllPath);
        // Environment.SetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR", doorStopManagedFolderPath);
        // Environment.SetEnvironmentVariable("DOORSTOP_PROCESS_PATH", doorStopProcessPath);
        // Environment.SetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS", doorStopDllSearchDirs);
        
        // var harmony = new Harmony("com.nomnom.test-bepinex");
        // harmony.PatchAll(typeof(SuppressSetPlatformThrowPath));
        // RunPreloader();
        // harmony.UnpatchSelf();
        
        var setExecutablePath = typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.NonPublic | BindingFlags.Static);
        setExecutablePath?.Invoke(null, new object[] { gameExePath, null, null, null });
        
        // reset logger sources
        var sources = typeof(BepInEx.Logging.Logger).GetProperty("Sources", BindingFlags.Public | BindingFlags.Static);
        var sourcesValue = (ICollection<ILogSource>)sources?.GetValue(null);
        sourcesValue?.Clear();
        
        // reset _Listeners
        var listeners = typeof(BepInEx.Logging.Logger).GetField("_Listeners", BindingFlags.NonPublic | BindingFlags.Static);
        var listenersValue = (ICollection<ILogListener>)listeners?.GetValue(null);
        listenersValue?.Clear();
        
        // reset Chainloader._initialized
        var initialized = typeof(Chainloader).GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);
        initialized?.SetValue(null, false);
        
        // reset _loaded
        var loaded = typeof(Chainloader).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
        loaded?.SetValue(null, false);
        
        // reset internalLogsInitialized
        var internalLogsInitialized = typeof(BepInEx.Logging.Logger).GetField("internalLogsInitialized", BindingFlags.NonPublic | BindingFlags.Static);
        internalLogsInitialized?.SetValue(null, false);
        
        // change UnityLogListener.WriteStringToUnityLog to debug log
        var writeStringToUnityLog = typeof(UnityLogListener).GetField("WriteStringToUnityLog", BindingFlags.NonPublic | BindingFlags.Static);
        writeStringToUnityLog?.SetValue(null, new Action<string>(Debug.Log));
        
        // var harmony = new Harmony("com.nomnom.test-bepinex");
        // harmony.PatchAll(typeof(SuppressSetPlatformThrowPath));
        // RunPreloader();
        // harmony.UnpatchSelf();
        
        var harmony = new Harmony("com.nomnom.test-bepinex");
        if (LoadProjectPlugins) {
            harmony.PatchAll(typeof(FindPluginTypesPatch));
        }
        harmony.PatchAll(typeof(OverrideEditorCheckPatch));
        harmony.PatchAll(typeof(NetworkManagerPatch));
        
        // let up do something dumb! :)
        // get all dlls from game's plugins and manually load them since
        // sub-dependencies are not always loaded
        var gameDlls = Directory.GetFiles(ModuleUtility.GamePluginsPath, "*.dll", SearchOption.AllDirectories);
        _assemblies.Clear();
        
        foreach (var gameDll in gameDlls) {
            Debug.Log($"Loading \"{gameDll}\"");
            try {
                _assemblies.Add(Assembly.LoadFile(gameDll));
            } catch (Exception e) {
                Debug.LogError($"Failed to load {gameDll}: {e}");
            }
        }
        
        // var dllPaths = Utility.GetUniqueFilesInDirectories(new[] { gamePlugins }, "*.dll");
        // var assemblies = dllPaths
        //     .Select(x => (x, AssemblyDefinition.ReadAssembly(x, TypeLoader.ReaderParameters)))
        //     .Distinct()
        //     .Where(x => x.Item2.Modules.Any(y => y.Types.Any(z => z.BaseType?.FullName == "BepInEx.BaseUnityPlugin")));
        //
        // var alreadyLoadedAssemblies = new List<string>();
        // foreach (var assembly in AccessTools.AllAssemblies()) {
        //     alreadyLoadedAssemblies.Add(assembly.FullName);
        // }

        // AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        // AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        //
        // foreach (var gameDll in gameDlls) {
        //     Debug.Log($"Loading \"{gameDll}\"");
        //     try {
        //         _assemblies.Add(Assembly.LoadFile(gameDll));
        //     } catch (Exception e) {
        //         Debug.LogError($"Failed to load {gameDll}: {e}");
        //     }
        // }
        
        // foreach (var (file, assemblyDefinition) in assemblies) {
        //     Debug.Log($"Loading \"{file}\"");
        //     if (alreadyLoadedAssemblies.Contains(assemblyDefinition.FullName)) {
        //         Debug.Log($"Already loaded {assemblyDefinition.FullName}");
        //         assemblyDefinition.Dispose();
        //         continue;
        //     }
        //     
        //     try {
        //         var assembly = Assembly.LoadFile(file);
        //         foreach (var child in assembly.GetReferencedAssemblies()) {
        //             try {
        //                 Debug.Log($"Loading child \"{child}\"");
        //                 if (alreadyLoadedAssemblies.Contains(child.FullName)) {
        //                     Debug.Log($"Already loaded {child.FullName}");
        //                     continue;
        //                 }
        //                 
        //                 var childAssembly = Assembly.Load(child);
        //                 _assemblies.Add(childAssembly);
        //                 alreadyLoadedAssemblies.Add(child.FullName);
        //             } catch (Exception e) {
        //                 Debug.LogError($"Failed to load {child}: {e}");
        //             }
        //         }
        //         _assemblies.Add(assembly);
        //     } catch (Exception e) {
        //         Debug.LogError($"Failed to load {file}: {e}");
        //     }
        //     
        //     alreadyLoadedAssemblies.Add(assemblyDefinition.FullName);
        //     assemblyDefinition.Dispose();
        // }
        // AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

        Chainloader.Initialize(gameExePath, false);
        
        OverrideBepInExPaths();
        
        // var patchAndLoadFunction = AccessTools.Method(typeof(AssemblyPatcher), nameof(AssemblyPatcher.PatchAndLoad));
        // patchAndLoadFunction?.Invoke(null, new object[] { new string[] {
        //     Path.GetFullPath(ManagedPath), gamePlugins
        // } });
        
        switch (BepInExLocation) {
            case BepInExLocation.Local:
                Debug.Log($"Using the local BepInEx folder");
                OverridePaths();
                break;
            case BepInExLocation.Custom:
                Debug.Log("Using a custom BepInEx folder");
                break;
            default:
                Debug.Log("Using the normal game's BepInEx folder");
                break;
        }
        
        AssignManagedData(gamePlugins);
        
        // if (HasDomainReloadingDisabled) {
        //     try {
        //         harmony.PatchAll(typeof(IgnoreILHelpersPatch));
        //     } catch (Exception e) {
        //         Debug.LogWarning($"Failed to patch IL helpers: {e}");
        //     }
        // }
        
        // harmony.PatchAll(typeof(BepInPlugin_Awake_Prefix));
        Chainloader.Start();
        
        harmony.UnpatchSelf();
        
        harmony.PatchAll(typeof(DestroyPatch));
        harmony.PatchAll(typeof(NetworkManagerPatch));
        harmony.PatchAll(typeof(OnlineDisablerPatch));
        harmony.PatchAll(typeof(IntroSkipperPatch));
        harmony.PatchAll(typeof(MenuSkipper));
        harmony.PatchAll(typeof(EventSystemPatch));
        harmony.PatchAll(typeof(InfiniteHealthPatch));
        harmony.PatchAll(typeof(InfiniteStaminaPatch));
        harmony.PatchAll(typeof(CreditsPatch));
        harmony.PatchAll(typeof(AutoMoonLoaderPatch));
        
        Debug.Log($"Loaded BepInEx in {stopwatch.ElapsedMilliseconds}ms!");
        
        var obj = new GameObject("CustomPlugin");
        obj.AddComponent<BepInExPatcher>();
        DontDestroyOnLoad(obj);
        
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        
        if (LoadPosterizationShader) {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
    }
    
    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
        var assembly = ((AppDomain)sender).GetAssemblies().FirstOrDefault(x => x.FullName == args.Name);
        if (assembly != null) {
            Debug.Log($"> Resolved \"{args.Name}\" to {assembly}");
        } else {
            Debug.LogWarning($"> Failed to resolve \"{args.Name}\"");
        }
        return assembly;
    }

    // [MenuItem("Tools/Nomnom/HandlePreloader")]
    // public static void HandlePreloader() {
    //     var gameExePath = BepInExLocation switch {
    //         BepInExLocation.Local => FakeExePath,
    //         BepInExLocation.Game => ActualExePath,
    //         BepInExLocation.Custom => ActualExePath,
    //         _ => throw new ArgumentOutOfRangeException("BepInExLocation was given an invalid value")
    //     };
    //     
    //     var bepInExFolder = BepInExLocation switch {
    //         BepInExLocation.Local => Path.Combine(Path.GetDirectoryName(FakeExePath)!, "BepInEx"),
    //         BepInExLocation.Custom => CustomBepInExLocation,
    //         _ => Path.Combine(GameDataPath, "..", "BepInEx")
    //     };
    //     
    //     var doorStopInvokeDllPath = Path.GetFullPath(Path.Combine(bepInExFolder, "core", "BepInEx.Preloader.dll"));
    //     var doorStopManagedFolderPath = Path.GetFullPath(ManagedPath);
    //     var doorStopProcessPath = Path.GetFullPath(gameExePath);
    //     var doorStopDllSearchDirs = string.Join(Path.PathSeparator, new string[] {
    //         Path.GetFullPath(ManagedPath),
    //         // @"C:\Users\nomno\Documents\Modding\LethalCompany_v49\LethalCompany_PatcherTest\Library\ScriptAssemblies"
    //         // Path.GetFullPath(gamePlugins)
    //     });
    //     
    //     Debug.Log($"Setting DOORSTOP_INVOKE_DLL_PATH to \"{doorStopInvokeDllPath}\"");
    //     Debug.Log($"Setting DOORSTOP_MANAGED_FOLDER_DIR to \"{doorStopManagedFolderPath}\"");
    //     Debug.Log($"Setting DOORSTOP_PROCESS_PATH to \"{doorStopProcessPath}\"");
    //     Debug.Log($"Setting DOORSTOP_DLL_SEARCH_DIRS to \"{doorStopDllSearchDirs}\"");
    //     
    //     Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", doorStopInvokeDllPath);
    //     Environment.SetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR", doorStopManagedFolderPath);
    //     Environment.SetEnvironmentVariable("DOORSTOP_PROCESS_PATH", doorStopProcessPath);
    //     Environment.SetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS", doorStopDllSearchDirs);
    //     
    //     var harmony = new Harmony("com.nomnom.test-bepinex");
    //     harmony.PatchAll(typeof(SuppressSetPlatformThrowPath));
    //     RunPreloader();
    //     harmony.UnpatchSelf();
    // }
    
    // private static class BepInPlugin_Awake_Prefix {
    //     private static IEnumerable<MethodBase> TargetMethods() {
    //         var types = _assemblies.SelectMany(a => a.GetTypes());
    //         return types
    //             .Where(x => typeof(BepInPlugin).IsAssignableFrom(x))
    //             .Select(x => x.GetMethod("Awake", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static));
    //     }
    //     
    //     private static void Prefix(BepInPlugin __instance) {
    //         Debug.Log($"Loaded BepInEx plugin {__instance}");
    //     }
    // }
    
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    // private static void OnAssembliesLoaded() {
    //     var harmony = new Harmony("com.nomnom.test-bepinex");
    //     harmony.PatchAll(typeof(SuppressSetPlatformThrowPath));
    //     RunPreloader();
    //     harmony.UnpatchSelf();
    // }

    // private static void RunPreloader() {
    //     var preloaderAssembly = typeof(BepInEx.Preloader.EnvVars).Assembly;
    //     var entryPoint = preloaderAssembly.GetType("BepInEx.Preloader.Entrypoint");
    //     var main = entryPoint.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);
    //     main?.Invoke(null, null);
    //     
    //     // var preloader = typeof(BepInEx.Preloader.EnvVars).Assembly.GetType("BepInEx.Preloader.PreloaderRunner");
    //     // var preloaderPreMain = preloader.GetMethod("PreloaderPreMain", BindingFlags.Public | BindingFlags.Static);
    //     // preloaderPreMain?.Invoke(null, null);
    // }
    //
    // private static class SuppressSetPlatformThrowPath {
    //     public static MethodBase TargetMethod() {
    //         // get PlatformUtils.SetPlatform
    //         var assembly = typeof(BepInEx.Preloader.EnvVars).Assembly;
    //         var type = assembly.GetType("BepInEx.Preloader.PlatformUtils");
    //         var method = type.GetMethod("SetPlatform", BindingFlags.Public | BindingFlags.Static);
    //         return method;
    //     }
    //     
    //     public static Exception Finalizer() {
    //         return null;
    //     }
    // }
    //
    // private static void CallStaticConstructors() {
    //     foreach (var assembly in _assemblies) {
    //         foreach (var type in assembly.GetTypes()) {
    //             try {
    //                 type.TypeInitializer.Invoke(null, null);
    //                 Debug.Log($"Ran static constructor for {type.FullName}");
    //             } catch {
    //                 // ignored
    //             }
    //         }
    //     }
    //     
    //     var assemblies = AccessTools.AllAssemblies().ToArray();
    //     foreach (var pair in GetAllPluginAssemblies(onlyMods: true)) {
    //         var assembly = assemblies.FirstOrDefault(x => x.FullName == pair.assembly.FullName);
    //         if (assembly == null) {
    //             continue;
    //         }
    //         
    //         foreach (var type in assembly.GetTypes()) {
    //             try {
    //                 type.TypeInitializer.Invoke(null, null);
    //                 Debug.Log($"Ran static constructor for {type.FullName}");
    //             } catch {
    //                 // ignored
    //             }
    //         }
    //     }
    // }

    private static void OverridePaths() {
        // ExecutablePath
        var executablePathProperty = typeof(Paths).GetProperty("ExecutablePath");
        executablePathProperty?.SetValue(null, ModuleUtility.ActualExePath);
    }

    private static void OverrideBepInExPaths() {
        var bepInExFolder = ModuleUtility.BepInExFolder;
        var bepInExRootPath = typeof(Paths).GetProperty("BepInExRootPath", BindingFlags.Public | BindingFlags.Static);
        bepInExRootPath?.SetValue(null, bepInExFolder);
        
        var configPath = typeof(Paths).GetProperty("ConfigPath", BindingFlags.Public | BindingFlags.Static);
        configPath?.SetValue(null, Path.Combine(bepInExFolder, "config"));
        
        var bepInExConfigPath = typeof(Paths).GetProperty("BepInExConfigPath", BindingFlags.Public | BindingFlags.Static);
        bepInExConfigPath?.SetValue(null, Path.Combine(bepInExFolder, "config", "BepInEx.cfg"));
        
        var patcherPluginPath = typeof(Paths).GetProperty("PatcherPluginPath", BindingFlags.Public | BindingFlags.Static);
        patcherPluginPath?.SetValue(null, Path.Combine(bepInExFolder, "patchers"));
        
        var bepInExAssemblyDirectory = typeof(Paths).GetProperty("BepInExAssemblyDirectory", BindingFlags.Public | BindingFlags.Static);
        bepInExAssemblyDirectory?.SetValue(null, Path.Combine(bepInExFolder, "core"));
        
        var bepInExAssemblyPath = typeof(Paths).GetProperty("BepInExAssemblyPath", BindingFlags.Public | BindingFlags.Static);
        bepInExAssemblyPath?.SetValue(null, Path.Combine(Path.Combine(bepInExFolder, "core"), typeof(Paths).Assembly.GetName().Name + ".dll"));
        
        var cachePath = typeof(Paths).GetProperty("CachePath", BindingFlags.Public | BindingFlags.Static);
        cachePath?.SetValue(null, Path.Combine(bepInExFolder, "cache"));
    }

    private static void AssignManagedData(params string[] pluginsPaths) {
        // ManagedPath
        var managedPathProperty = typeof(Paths).GetProperty("ManagedPath");
        managedPathProperty?.SetValue(null, ModuleUtility.ManagedPath);
        
        // DllSearchPaths
        var dllSearchPathsProperty = typeof(Paths).GetProperty("DllSearchPaths");
        dllSearchPathsProperty?.SetValue(null, new[] {
            ModuleUtility.ManagedPath
        }.Concat(pluginsPaths)
        .ToArray());
    }

    private void OnDestroy() {
        Harmony.UnpatchAll();
        ResetNetcodeRpcTablesPatch.DidReset = false;

        // DisposePlugins();
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

    // private static void DisposePlugins() {
    //     // clear all plugin static information
    //     var types = _assemblies.SelectMany(a => a.GetTypes());
    //     // var finalTypes = new HashSet<Type>();
    //     foreach (var type in types) {
    //         DisposeType(type, null);
    //         // finalTypes.Add(type);
    //     }
    //
    //     var assemblies = AccessTools.AllAssemblies().ToArray();
    //     // var settings = ModuleUtility.GetPatcherSettings();
    //     // var modsDirectory = settings.GetModsPath().Replace("\\", "/");
    //
    //     foreach (var pair in GetAllPluginAssemblies(onlyMods: true)) {
    //         var assembly = assemblies.FirstOrDefault(x => x.FullName == pair.assembly.FullName);
    //         if (assembly == null) {
    //             continue;
    //         }
    //         
    //         Debug.Log($"Disposing {assembly.FullName}");
    //         foreach (var type in assembly.GetTypes()) {
    //             DisposeType(type, pair.assembly);
    //             // finalTypes.Add(type);
    //         }
    //         
    //         pair.assembly.Dispose();
    //     }
    //
    //     // foreach (var type in finalTypes) {
    //     //     Debug.Log($"Running static constructor for {type.FullName}");
    //     //     type.TypeInitializer.Invoke(null, null);
    //     // }
    //     
    //     // network manager disposal
    //     // var networkManagerObj = GameObject.Find("NetworkManager");
    //     // var networkManager = networkManagerObj.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == "NetworkManager");
    //     // if (!networkManager) {
    //     //     Debug.LogError("Failed to find NetworkManager!");
    //     //     return;
    //     // }
    //     //
    //     // var networkConfig = AccessTools.Field(networkManager.GetType(), "NetworkConfig");
    //     // var networkConfigValue = networkConfig.GetValue(networkManager);
    //     // var prefabs = AccessTools.Field(networkConfigValue.GetType(), "Prefabs");
    //     // var prefabsValue = prefabs.GetValue(networkConfigValue);
    //     // var m_Prefabs = AccessTools.Field(prefabsValue.GetType(), "m_Prefabs");
    //     // var iList = (IList)m_Prefabs.GetValue(prefabsValue);
    //     //
    //     // Debug.Log($"Disposing {iList.Count} prefabs if needed");
    //     // for (var i = 0; i < iList.Count; i++) {
    //     //     if (iList[i] == null) {
    //     //         Debug.Log($"Disposing prefab at {i}");
    //     //         iList.RemoveAt(i--);
    //     //     }
    //     // }
    // }
    //
    // private static void DisposeType(Type type, AssemblyDefinition assemblyDefinition) {
    //     var asmType = assemblyDefinition?.MainModule.Types
    //         .FirstOrDefault(x => x.Name == type.Name);
    //     var cctor = asmType?.Methods
    //         .SingleOrDefault(x => x.Name == ".cctor");
    //     var instructions = cctor?.Body.Instructions;
    //     
    //     var fields = type
    //         .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
    //         .Where(x => !x.Name.StartsWith("<"));
    //     foreach (var field in fields) {
    //         if (field.IsLiteral) continue;
    //         
    //         try {
    //             if (field.FieldType.IsValueType) {
    //                 if (instructions != null) {
    //                     var asmField = asmType.Fields.SingleOrDefault(x => x.Name == field.Name);
    //                     var store = instructions.SingleOrDefault(x => x.OpCode == OpCodes.Stsfld && x.Operand == asmField);
    //                     if (store != null) {
    //                         Debug.Log($"For value type {field.Name} found store at {store}");
    //                         continue;
    //                     }
    //                 }
    //
    //                 var defaultValue = Activator.CreateInstance(field.FieldType);
    //                 field.SetValue(null, defaultValue);
    //                 Debug.Log($"Reset value type \"{field.Name}\" in {type.FullName} to {defaultValue}");
    //             } else {
    //                 if (typeof(IList).IsAssignableFrom(field.FieldType)) {
    //                     var list = (IList)field.GetValue(null);
    //                     if (list != null) {
    //                         list.Clear();
    //                     } else {
    //                         field.SetValue(null, Activator.CreateInstance(field.FieldType));
    //                     }
    //                     Debug.Log($"Reset list type \"{field.Name}\" in {type.FullName}");
    //                     continue;
    //                 } else if (typeof(IDictionary).IsAssignableFrom(field.FieldType)) {
    //                     var dict = (IDictionary)field.GetValue(null);
    //                     if (dict != null) {
    //                         dict.Clear();
    //                     } else {
    //                         field.SetValue(null, Activator.CreateInstance(field.FieldType));
    //                     }
    //                     Debug.Log($"Reset dictionary type \"{field.Name}\" in {type.FullName}");
    //                     continue;
    //                 } else if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) {
    //                     field.SetValue(null, null);
    //                     Debug.Log($"Reset reference type \"{field.Name}\" in {type.FullName}");
    //                     continue;
    //                 }
    //                 
    //                 if (instructions != null) {
    //                     var asmField = asmType.Fields.SingleOrDefault(x => x.Name == field.Name);
    //                     var store = instructions.SingleOrDefault(x => x.OpCode == OpCodes.Stsfld && x.Operand == asmField);
    //                     if (store != null) {
    //                         Debug.Log($"For ref type {field.Name} found store at {store}");
    //                         continue;
    //                     }
    //                 }
    //
    //                 // try {
    //                 //     var instance = Activator.CreateInstance(field.FieldType);
    //                 //     field.SetValue(null, instance);
    //                 // } catch {
    //                 //     field.SetValue(null, null);
    //                 // }
    //                 
    //                 field.SetValue(null, null);
    //                 Debug.Log($"Reset reference type \"{field.Name}\" in {type.FullName}");
    //             }
    //         } catch {
    //             // Debug.LogWarning($"Failed to reset {field.Name} in {type.FullName}: {e}");
    //         }
    //     }
    // }
    
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        // var useExperimentalPosterizationShader = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_experimental_posterization_shader", false);
        if (scene.name == "SampleSceneRelay" && !_posterizationVolume && LoadPosterizationShader) {
            var volume = Resources.Load<CustomPassVolume>("Posterization/PosterizationGlobalVolume");
            if (!volume) {
                Debug.LogWarning("Failed to load PosterizationGlobalVolume!");
                return;
            }
            
            _posterizationVolume = Instantiate(volume);
            _posterizationVolume.gameObject.SetActive(true);
            SceneManager.MoveGameObjectToScene(_posterizationVolume.gameObject, scene);
        }
    }
    
    private static void OnSceneUnloaded(Scene scene) {
        if (scene.name == "SampleSceneRelay" && _posterizationVolume) {
            Destroy(_posterizationVolume.gameObject);
            _posterizationVolume = null;
        }
    }

    public static IEnumerable<(string file, AssemblyDefinition assembly)> GetAllPluginAssemblies(bool onlyMods = false) {
        var settings = ModuleUtility.GetPatcherSettings();
        var modsDirectory = settings.GetModsPath().Replace("\\", "/");
        modsDirectory = modsDirectory.Replace('/', Path.DirectorySeparatorChar);

        var files = AccessTools
            .AllTypes()
            .Select(x => x.Module.FullyQualifiedName)
            .Where(x => (!onlyMods && x.Contains("Assembly-CSharp")) || x.Contains(modsDirectory))
            .Distinct()
            .Select(x => (x, AssemblyDefinition.ReadAssembly(x, TypeLoader.ReaderParameters)));
        
        return files;
    }
}
#endif
