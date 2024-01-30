## To get BepInEx to work
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

## To get patching functional

```csharp
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using DefaultNamespace.Scripts;
using HarmonyLib;
using MonoMod.Utils;
using UnityEngine;

namespace DefaultNamespace {
    public class TestBepInEx: MonoBehaviour {
        private Harmony _harmony;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnLoad() {
            var exePath = @"C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\Lethal Company.exe";
            var setExecutablePath = typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.NonPublic | BindingFlags.Static);
            setExecutablePath.Invoke(null, new object[] {exePath, null, null, null});
            
            try {
                Chainloader.Initialize(exePath, false);
            } catch (Exception e) {
                Debug.LogError(e);
            }
        }
        
        private void Awake() {
            _harmony = new Harmony("TestMod");
            _harmony.PatchAll();
            DontDestroyOnLoad(gameObject);
            Debug.Log("Hello from TestBepInEx!");
        }

        private void OnDestroy() {
            _harmony.UnpatchSelf();
            Harmony.UnpatchAll();
            Debug.Log("Goodbye from TestBepInEx!");
        }
    }
}
```

When adding a new plugin, you might have to reload unity?