using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    [Serializable]
    public struct ShaderInjection {
        public string ShaderName;
        public string BundleName;
        public List<Shader> DummyShaders;
        public List<Material> Materials;

        [NonSerialized, CanBeNull]
        public Shader InjectedShader;

        public Shader GetInjectedShader() {
            // Load bundle
            var bundlePath = Path.Join(Application.streamingAssetsPath, "ShaderInjections",
                $"{BundleName}.shaderinject");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            InjectedShader = bundle.LoadAsset<Shader>($"assets/injectedshaders/{BundleName}.shader");

            // Unload
            bundle.Unload(false);

            return InjectedShader;
        }
        
        public bool MaterialNeedsInjection(Material material) {
            // null/internalerrorshader happens when it was serialized using a temporary injected shader
            if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader") {
                return true;
            }
            // shaders are initially using AssetRipper's 'Dummy' shaders
            if (DummyShaders.Any(x => x == material.shader)) {
                return true;
            }

            return false;
        }
    }
}