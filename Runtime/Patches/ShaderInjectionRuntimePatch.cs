using UnityEngine;

namespace Nomnom.LCProjectPatcher.Patches {
    // Essentially just trying to get ShaderInjection behaviour in builds without having to directly interact with the game
    // This should work completely generically
    public class ShaderInjectionRuntimePatch {
        #if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            var injectionSettings = Resources.Load<LCPatcherShaderInjectionSettings>("ShaderInjectionSettings");
            if (injectionSettings == null) {
                return;
            }
            
            Debug.Log("Applying all injected shaders.");
            foreach (var shaderInjection in injectionSettings.ShaderInjections) {
                foreach (var material in shaderInjection.Materials) {
                    if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader") {
                        material.shader = shaderInjection.GetInjectedShader();
                    }
                }
            }
        }
        #endif
    }
}