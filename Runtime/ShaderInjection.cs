using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    [Serializable]
    public struct ShaderInjection {
        public string ShaderName;
        public string BundleName;
        public Shader DummyShader;
        public List<Material> Materials;

        [NonSerialized, CanBeNull]
        public Shader InjectedShader;

        public Shader GetInjectedShader() {
            if (InjectedShader != null)
                return InjectedShader;
            
            // Load bundle
            var bundlePath = Path.Join(Application.streamingAssetsPath, "ShaderInjections",
                $"{BundleName}.shaderinject");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            InjectedShader = bundle.LoadAsset<Shader>($"assets/injectedshaders/{BundleName}.shader");

            // Unload
            bundle.Unload(false);

            return InjectedShader;
        }
    }
}