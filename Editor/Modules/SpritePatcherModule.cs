using Cysharp.Threading.Tasks;
using UnityEditor;

namespace Nomnom.LCProjectPatcher.Modules {
    public static class SpritePatcherModule {
        public static async UniTask Patch() {
            var sprites = AssetDatabase.FindAssets("t:Sprite");
            foreach (var guid in sprites) {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                // todo: convert each child sprite that imported as a normal sprite asset into an actual sprite asset
            }
        }
    }
}
