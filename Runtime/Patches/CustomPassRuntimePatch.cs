using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Nomnom.LCProjectPatcher.Patches {
    // Apply custom pass (not sure why it gets nuked in the project?)
    // Ideally this would be sorted upon project creation, but for now I'll do this
    public class CustomPassRuntimePatch {
        [CanBeNull]
        private static LCPatcherShaderInjectionSettings _injectionSettings = null;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // A bit slow but it should do the job just fine
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            // Get shader injection settings so we can get the injected posterization material
            _injectionSettings = _injectionSettings ?? Resources.Load<LCPatcherShaderInjectionSettings>("ShaderInjectionSettings");
            var passMaterial = _injectionSettings?.ShaderInjections
                .Where(shaderInjection => shaderInjection.ShaderName == "Shader Graphs/PosterizationFilter")
                .SelectMany(shaderInjection => shaderInjection.Materials)
                .FirstOrDefault();
            if (_injectionSettings == null || passMaterial == null) {
                return;
            }
            
            // Find existing base game custom pass
            var customPassGameObject = GameObject.Find("Systems/Rendering/CustomPass");
            var customPassVolume = customPassGameObject?.GetComponent<CustomPassVolume>();
            if (customPassVolume == null || customPassVolume.customPasses.Count > 0) {
                return;
            }

            // Create the actual custom pass, with base game (or near base game, i haven't checked 1000%) values
            var drawRenderersCustomPass = new DrawRenderersCustomPass();
            drawRenderersCustomPass.layerMask = ~0; // LayerMask.Everything
            drawRenderersCustomPass.overrideMaterial = passMaterial;
            drawRenderersCustomPass.overrideMaterialPassName = "ForwardOnly";
            drawRenderersCustomPass.depthWrite = true;
            drawRenderersCustomPass.overrideDepthState = true;
            drawRenderersCustomPass.depthCompareFunction = CompareFunction.Equal;
            drawRenderersCustomPass.sortingCriteria = SortingCriteria.CommonOpaque;
            customPassVolume.customPasses.Add(drawRenderersCustomPass);
        }
    }
}