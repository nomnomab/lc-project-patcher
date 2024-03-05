﻿using System.Collections.Generic;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    // note: needs to be in Resources to be picked up in runtime builds
    [CreateAssetMenu(fileName = "ShaderInjectionSettings", menuName = "LC Project Patcher/Shader Injection Settings")]
    public class LCPatcherShaderInjectionSettings: ScriptableObject {
        public List<ShaderInjection> ShaderInjections = new();

        public void InjectAllShadersIntoMaterials() {
            foreach (var shaderInjection in ShaderInjections) {
                foreach (var material in shaderInjection.Materials) {
                    if (shaderInjection.MaterialNeedsInjection(material)) {
                        material.shader = shaderInjection.GetInjectedShader();
                    }
                }
            }
        }
    }
}