using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    [Serializable]
    public struct ShaderInjection {
        public string ShaderName;
        public string BundleName;
        public Shader DummyShader;
        public List<Material> Materials;
    }
}