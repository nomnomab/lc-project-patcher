## Asset Ripper needs

- Scripting mode: `Decompiled`
- Scripting level: `Level2`

## To get BepInEx & Harmony to work
- download BepInEx from https://github.com/BepInEx/BepInEx/releases/tag/v5.4.22
- copy over:
    - 0Harmony
    - BepInEx
    - BepInEx.Harmony
        - exclude from all platforms (important)
    - BepInEx.Preloader
    - HarmonyXInterop
    - Mono.Cecil
    - Mono.Cecil.Mdb
    - Mono.Cecil.Pdb
    - Mono.Cecil.Rocks
- download MonoMod from https://github.com/MonoMod/MonoMod/releases/tag/v22.07.31.01
- copy over:
    - MonoMod.DebugIL
    - MonoMod
    - MonoMod.RuntimeDetour
    - MonoMod.RuntimeDetour.HookGen
    - MonoMod.Utils

- When wanting to remove a plugin, the editor has to be closed so the dll can be unloaded
  - Unity why are you like this
- Convert negative scales/box collider sizes to positive

## To get patching functional and to fix BepInEx with in-project plugins

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using DefaultNamespace.Scripts;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace DefaultNamespace {
    public static class BepInExPatcher {
        private const string ActualGamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\Lethal Company.exe";
        
        private static string FakeExePath => Path.Combine(Application.dataPath, "..", "Lethal Company", "Lethal Company.exenot");
        private static string DirectoryPath => Path.GetDirectoryName(ActualGamePath);
        private static string GameDataPath => Path.Combine(DirectoryPath, "Lethal Company_Data");
        private static string ManagedPath => Path.Combine(GameDataPath, "Managed");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnLoad() {
            var setExecutablePath = typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.NonPublic | BindingFlags.Static);
            setExecutablePath.Invoke(null, new object[] {FakeExePath, null, null, null});
            
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
            
            Chainloader.Initialize(FakeExePath, false);
            OverridePaths();
            // DebugPaths();
            
            Chainloader.Start();
            harmony.UnpatchSelf();
            
            Debug.Log("Loaded BepInEx!");
        }

        private static void OverridePaths() {
            // ExecutablePath
            var executablePathProperty = typeof(Paths).GetProperty("ExecutablePath");
            executablePathProperty.SetValue(null, ActualGamePath);
            
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
    }

    [HarmonyPatch]
    public static class FindPluginTypesPatch {
        public static MethodBase TargetMethod() {
            return AccessTools
                .Method(typeof(TypeLoader), nameof(TypeLoader.FindPluginTypes))
                .MakeGenericMethod(typeof(PluginInfo));
        }
        
        public static Dictionary<string, List<PluginInfo>> Postfix(Dictionary<string, List<PluginInfo>> result) {
            var file = typeof(BepInExPatcher).Module.FullyQualifiedName;
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(file, TypeLoader.ReaderParameters);
            var hasBepinPluginsFunction = AccessTools.Method(typeof(Chainloader), "HasBepinPlugins");
            
            if (!(bool)hasBepinPluginsFunction.Invoke(null, new object[] {assemblyDefinition})) {
                result[file] = new List<PluginInfo>();
                assemblyDefinition.Dispose();
            } else {
                var list = assemblyDefinition.MainModule.Types
                    .Select<TypeDefinition, PluginInfo>(Chainloader.ToPluginInfo)
                    .Where(t => (object)t != null)
                    .ToList();
                result[file] = list;
                assemblyDefinition.Dispose();
            }
            
            return result;
        }
    }
}
```

## To fix NetCode being dumb with domain reloading off

```csharp
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

// ? eat my socks Unity and your lack of using domain reload
public static class ResetNetcodeRpcTables {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoadRuntimeMethod() {
        ResetRpcFuncTable();
    }
        
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void OnInitializeOnLoadMethod() {
        ResetRpcFuncTable();
    }
#endif

    private static void ResetRpcFuncTable() {
        var rpcFuncTableField = typeof(NetworkManager).GetField("__rpc_func_table");
        var rpcNameTableField = typeof(NetworkManager).GetField("__rpc_name_table");
        var rpcFuncTable = (IDictionary)rpcFuncTableField.GetValue(null);
        var rpcNameTable = (IDictionary)rpcNameTableField.GetValue(null);
        rpcFuncTable.Clear();
        rpcNameTable.Clear();
        rpcFuncTableField.SetValue(null, rpcFuncTable);
        rpcNameTableField.SetValue(null, rpcNameTable);
        // Debug.Log("Reset rpc_func_table and rpc_name_table.");
    }
}
```