using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Nomnom.LCProjectPatcher.Editor.Modules;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Editor {
    [InitializeOnLoad]
    public class ShaderInjectionEditorPatch : AssetPostprocessor {
        [CanBeNull]
        private static LCPatcherShaderInjectionSettings _shaderInjectionSettings = null;
        
        // Called in editor startup and domain reload, every injected shader likely got saved as null
        // So we'll have to re-initialize all of them
        static ShaderInjectionEditorPatch() { 
            var injectionSettings = TryGetInjectionSettings();
            if (injectionSettings == null) {
                return;
            }

            foreach (var shaderInjection in injectionSettings.ShaderInjections) {
                foreach (var material in shaderInjection.Materials) {
                    if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader") {
                        material.shader = shaderInjection.GetInjectedShader();
                    }
                }
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            string[] materials = importedAssets.Where(path => path.EndsWith(".mat")).ToArray();

            // We're only looking for modified materials with injected values
            if (materials.Length == 0)
                return;

            var injectionSettings = TryGetInjectionSettings();
            if (injectionSettings == null) {
                return;
            }

            Dictionary<ShaderInjection, List<Material>> updatedMaterialsByShaderInjection = new();
            foreach (var shaderInjection in injectionSettings.ShaderInjections) {
                foreach (var injectedMaterial in shaderInjection.Materials) {
                    var injectedMaterialPath = AssetDatabase.GetAssetPath(injectedMaterial);
                    foreach (var material in materials) {
                        if (injectedMaterialPath == material && (injectedMaterial.shader == null || injectedMaterial.shader.name == "Hidden/InternalErrorShader")) {
                            if (!updatedMaterialsByShaderInjection.TryGetValue(shaderInjection, out List<Material> updatedMaterials)) {
                                updatedMaterials = new();
                            }
                            updatedMaterials.Add(injectedMaterial);
                            updatedMaterialsByShaderInjection[shaderInjection] = updatedMaterials;
                        }
                    }
                }
            }
            
            foreach (var (shaderInjection, updatedMaterials) in updatedMaterialsByShaderInjection) {
                // Set material shaders 
                foreach (var material in updatedMaterials) {
                    material.shader = shaderInjection.GetInjectedShader();
                }
            }
        }

        private static LCPatcherShaderInjectionSettings TryGetInjectionSettings() {
            // TODO: look into the performance implications of this
            if (_shaderInjectionSettings == null) {
                _shaderInjectionSettings = Resources.Load<LCPatcherShaderInjectionSettings>("ShaderInjectionSettings");
            }

            return _shaderInjectionSettings;
        }
    }
}