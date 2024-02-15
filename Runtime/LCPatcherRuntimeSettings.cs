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
        
        [Header("Experimental")]
        public bool LoadPosterizationShader;
    }
}
