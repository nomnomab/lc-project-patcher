using System.Collections.Generic;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    // note: needs to be in Resources to be picked up in runtime builds
    [CreateAssetMenu(fileName = "ShaderInjectionSettings", menuName = "LC Project Patcher/Shader Injection Settings")]
    public class LCPatcherShaderInjectionSettings: ScriptableObject {
        public bool EnableShaderInjections = true;
        public List<ShaderInjection> ShaderInjections = new();
    }
}