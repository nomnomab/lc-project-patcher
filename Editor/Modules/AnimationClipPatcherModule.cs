using System.IO;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Modules {
    public static class AnimationClipPatcherModule {
        public readonly struct GuidElement {
            public readonly string fileId;
            public readonly string guid;
            public readonly string type;
            
            public GuidElement(string fileId, string guid, string type) {
                this.fileId = fileId;
                this.guid = guid;
                this.type = type;
            }
        }
        
        private readonly static Regex PptrCurveMappingItemPattern = new(@"- {fileID: (?<file>\d+), guid: (?<guid>[0-9A-f-a-f]+), type: (?<type>\d+)}", RegexOptions.Compiled);
        
        public static UniTask Patch() {
            var assetRipperPath = EditorPrefs.GetString("nomnom.lc_project_patcher.asset_ripper_path");
            var projectPath = Path.Combine(Application.dataPath, "Scripts", "Assembly-CSharp");
            var animationClipPath = Path.Combine(assetRipperPath, "Assets", "AnimationClip");
            
            var animationClipFiles = Directory.GetFiles(animationClipPath, "*.anim", SearchOption.AllDirectories);
            foreach (var animationClipFile in animationClipFiles) {
                var fileName = Path.GetFileName(animationClipFile);
                // todo: convert m_FloatCurves to m_PPtrCurves if isPPtrCurve is true for a given clip
            }
            
            return UniTask.CompletedTask;
        }
    }
}
