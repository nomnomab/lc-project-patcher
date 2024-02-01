using System;
using System.IO;
using System.Linq;
using Nomnom.LCProjectPatcher.Attributes;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    [Serializable]
    public class AssetRipperSettings {
        public string BaseOutputPath => _baseOutputPath;
        public FolderMapping[] FolderMappings => _folderMappings;

        [SerializedPath(nameof(LCPatcherSettings.GetBaseLethalCompanyPath))]
        [SerializeField] 
        private string _baseOutputPath = "Game";
        
        [SerializeField] 
        private FolderMapping[] _folderMappings = new [] {
            new FolderMapping("AnimationClip", Path.Combine("Animation", "AnimationClips")),
            new FolderMapping("AnimationController", Path.Combine("Animation", "AnimationControllers")),
            new FolderMapping("AudioClip", Path.Combine("Audio", "AudioClips")),
            new FolderMapping("AudioMixerController", Path.Combine("Audio", "AudioMixerControllers")),
            new FolderMapping("Font", Path.Combine("Fonts", "TextMeshPro")),
            new FolderMapping("LightingSettings"),
            new FolderMapping("Material", "Materials"),
            new FolderMapping("Mesh", "Meshes"),
            new FolderMapping("PrefabInstance", "Prefabs"),
            new FolderMapping("PhysicsMaterial", "PhysicsMaterials"),
            new FolderMapping("Resources"),
            new FolderMapping("Settings"),
            new FolderMapping("Scenes"),
            new FolderMapping("NavMeshData", Path.Combine("Scenes", "NavMeshData")),
            new FolderMapping("Cubemap", Path.Combine("Scenes", "Cubemaps")),
            new FolderMapping("TerrainData", Path.Combine("Scenes", "TerrainData")),
            new FolderMapping("Shader", "Shaders"),
            new FolderMapping("Scripts"),
            new FolderMapping("Texture2D", Path.Combine("Textures", "Texture2Ds")),
            new FolderMapping("Texture3D", Path.Combine("Textures", "Texture3Ds")),
            new FolderMapping("RenderTexture", Path.Combine("Textures", "RenderTextures")),
            new FolderMapping("TerrainLayer", Path.Combine("Textures", "TerrainLayers")),
            new FolderMapping("Sprite", Path.Combine("Textures", "Sprites")),
            new FolderMapping("VideoClip", "Videos"),
        }.OrderBy(x => x.folderPath).ToArray();
        
        public bool TryGetMapping(string folderPath, out string outputPath) {
            foreach (var folderMapping in _folderMappings) {
                if (folderMapping.folderPath == folderPath) {
                    outputPath = folderMapping.outputPath;
                    return true;
                }
            }

            outputPath = null;
            return false;
        }
    }
}
