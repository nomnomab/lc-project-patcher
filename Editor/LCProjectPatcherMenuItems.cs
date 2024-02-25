using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nomnom.LCProjectPatcher.Editor.Modules;
using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Nomnom.LCProjectPatcher.Editor {
    public static class LCProjectPatcherMenuItems {
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Open", priority = 1)]
        public static void ShowWindow() {
            var window = EditorWindow.GetWindow<LCProjectPatcherEditorWindow>("LC - Project Patcher");
            window.minSize = new UnityEngine.Vector2(400, 400);
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Open Runtime Settings", priority = 2)]
        public static void ShowRuntimeSettings() {
            var settings = ModuleUtility.GetPatcherRuntimeSettings();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = settings;
        }
        
        [MenuItem("Assets/Split Combined Meshes in Scenes")]
        public static void SplitCombinedMesh() {
            if (!Selection.activeObject) return;
            var selection = Selection.objects
                .Where(x => x is Mesh mesh && mesh.subMeshCount > 1)
                .Cast<Mesh>()
                .ToArray();
            
            if (selection.Length == 0) {
                EditorUtility.DisplayDialog("Error", "No combined meshes selected", "Ok");
                return;
            }
            
            var settings = ModuleUtility.GetPatcherSettings();
            string scenesPath;
            if (settings.AssetRipperSettings.TryGetMapping("Scenes", out var finalFolder)) {
                scenesPath = Path.Combine(settings.GetLethalCompanyGamePath(), finalFolder);
            } else {
                scenesPath = Path.Combine(settings.GetLethalCompanyGamePath(), "Scenes");
            }
            
            var allScenes = AssetDatabase.FindAssets("t:Scene", new string[] { scenesPath });
            var currentScene = EditorSceneManager.GetActiveScene();
            foreach (var sceneGuid in allScenes) {
                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                EditorUtility.DisplayProgressBar("Splitting Combined Mesh", $"Processing scene {scenePath}", 0);
                Scene scene = default;
                
                if (currentScene.path == scenePath) {
                    scene = currentScene;
                } else {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
                
                SplitMeshesInScene(scene);
                
                if (currentScene.isDirty) {
                    EditorUtility.DisplayProgressBar("Splitting Combined Mesh", "Saving scene", 1);
                    AssetDatabase.SaveAssets();
                }
                if (currentScene.path != scenePath) {
                    EditorUtility.DisplayProgressBar("Splitting Combined Mesh", "Closing scene", 1);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            
            EditorUtility.ClearProgressBar();
        }
        
        [MenuItem("Assets/Split Combined Meshes in Scenes", isValidateFunction: true)]
        public static bool SplitCombinedMeshValidate() {
            if (!Selection.activeObject) return false;
            var selection = Selection.objects
                .Where(x => x is Mesh mesh && mesh.subMeshCount > 1 && mesh.name.StartsWith("Combined Mesh"))
                .Cast<Mesh>()
                .ToArray();
            return selection.Length > 0;
        }

        private static void SplitMeshesInScene(Scene scene, MeshFilter selectedMeshFilter = null) {
            var scenePath = scene.path;
            var selection = Selection.objects
                .Where(x => x is Mesh mesh && mesh.subMeshCount > 1)
                .Cast<Mesh>()
                .ToArray();

            using var _ = DictionaryPool<Mesh, (string path, string name)>.Get(out var meshPaths);
            foreach (var mesh in selection) {
                // create folder
                var meshFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(mesh));
                var meshName = mesh.name.Replace(' ', '_').Replace('(', '-').Replace(')', '-').Replace(':', '-');
                var meshFolderName = $"{meshName}_submeshes";

                string folderPath;
                if (!AssetDatabase.IsValidFolder($"{meshFolder}/{meshFolderName}")) {
                    folderPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(meshFolder, meshFolderName));
                } else {
                    folderPath = $"{meshFolder}/{meshFolderName}";
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Created submesh folder for {mesh.name} @ {folderPath}");
                meshPaths.Add(mesh, (folderPath, meshName));
            }
            
            if (selectedMeshFilter) {
                HandleMeshFilter(selectedMeshFilter, selection, scenePath, meshPaths);
                EditorUtility.ClearProgressBar();
                return;
            }

            var sceneObjects = scene.GetRootGameObjects();
            for (var i = 0; i < sceneObjects.Length; i++) {
                var sceneObject = sceneObjects[i];
                EditorUtility.DisplayProgressBar("Splitting Combined Mesh", $"Processing scene {scenePath}", (float)i / sceneObjects.Length);

                var meshFilters = sceneObject.GetComponentsInChildren<MeshFilter>();
                for (var j = 0; j < meshFilters.Length; j++) {
                    var meshFilter = meshFilters[j];
                    EditorUtility.DisplayProgressBar("Splitting Combined Mesh", $"Processing meshFilter {meshFilter.name}", (float)j / meshFilters.Length);

                    HandleMeshFilter(meshFilter, selection, scenePath, meshPaths);
                }
            }
        }

        private static void HandleMeshFilter(MeshFilter meshFilter, Mesh[] selection, string scenePath, Dictionary<Mesh, (string path, string name)> meshPaths) {
            var meshRenderer = meshFilter.GetComponent<MeshRenderer>();

            for (var k = 0; k < selection.Length; k++) {
                var mesh = selection[k];
                EditorUtility.DisplayProgressBar("Splitting Combined Mesh", $"Checking for {mesh.name}", (float)k / selection.Length);

                if (meshFilter.sharedMesh != mesh) {
                    continue;
                }

                if (meshFilter.TryGetComponent(out MeshCollider meshCollider) && meshCollider.sharedMesh) {
                    var meshColliderMeshPath = AssetDatabase.GetAssetPath(meshCollider.sharedMesh);
                    AssetDatabase.RenameAsset(meshColliderMeshPath, meshCollider.gameObject.name);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    meshFilter.sharedMesh = meshCollider.sharedMesh;
                    Debug.Log($"Found mesh \"{mesh.name}\" at {meshCollider} in scene {scenePath}");
                    break;
                }

                Debug.Log($"Found mesh \"{mesh.name}\" at {meshFilter} in scene {scenePath}");

                var submeshIndex = meshRenderer.subMeshStartIndex;
                var subMesh = GetSubMesh(meshFilter.sharedMesh, submeshIndex);

                var verts = subMesh.vertices;
                var position = meshFilter.transform.position;
                for (var l = 0; l < verts.Length; l++) {
                    var vert = verts[l];
                    verts[l] = meshFilter.transform.InverseTransformPoint(vert);
                    EditorUtility.DisplayProgressBar("Splitting Combined Mesh", $"Fixing verts", (float)l / verts.Length);
                }
                subMesh.name = $"{meshFilter.gameObject.name}_submesh";
                subMesh.vertices = verts;
                subMesh.RecalculateNormals();
                subMesh.RecalculateBounds();

                // save mesh to disk
                var (path, name) = meshPaths[mesh];
                var assetPath = $"{path}/{subMesh.name}.asset".Replace("\\", "/");
                AssetDatabase.CreateAsset(subMesh, assetPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                meshFilter.sharedMesh = meshAsset;

                Debug.Log($"Saved submesh {submeshIndex} of {mesh.name} to {assetPath}");

                EditorUtility.SetDirty(meshFilter);

                break;
            }
        }

        [MenuItem("CONTEXT/MeshFilter/Split Combined Mesh")]
        public static void SplitCombinedMeshContext(MenuCommand command) {
            var meshFilter = (MeshFilter) command.context;
            var meshAsset = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAsset);
            
            Selection.activeObject = mesh;

            var scene = EditorSceneManager.GetActiveScene();
            SplitMeshesInScene(scene, meshFilter);
            
            EditorUtility.ClearProgressBar();
            
            Selection.activeObject = meshFilter;
        }
        
        [MenuItem("CONTEXT/MeshFilter/Split Combined Mesh", isValidateFunction: true)]
        [MenuItem("CONTEXT/MeshFilter/Split Combined Mesh in Scene", isValidateFunction: true)]
        public static bool SplitCombinedMeshContextValidate(MenuCommand command) {
            var meshFilter = (MeshFilter) command.context;
            var meshAsset = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            if (string.IsNullOrEmpty(meshAsset)) {
                return false;
            }

            if (!meshFilter.gameObject.scene.isLoaded) {
                return false;
            }
            
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAsset);
            return mesh.subMeshCount > 1 && mesh.name.StartsWith("Combined Mesh");
        }
        
        [MenuItem("CONTEXT/MeshFilter/Split Combined Mesh in Scene")]
        public static void SplitCombinedMeshInSceneContext(MenuCommand command) {
            var meshFilter = (MeshFilter) command.context;
            var meshAsset = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAsset);
            
            Selection.activeObject = mesh;

            var scene = EditorSceneManager.GetActiveScene();
            SplitMeshesInScene(scene);
            
            EditorUtility.ClearProgressBar();

            Selection.activeObject = meshFilter;
        }

        private static Mesh GetSubMesh(Mesh mesh, int subMesh) {
            var meshName = mesh.name.Replace(' ', '_').Replace('(', '-').Replace(')', '-').Replace(':', '-');

            // var meshVerts = mesh.vertices;
            // var meshNormals = mesh.normals;
            // var meshUvs = mesh.uv;
            //
            // var submesh = mesh.GetSubMesh(subMesh);
            // var verts = new List<Vector3>();
            // var normals = new List<Vector3>();
            // var uvs = new List<Vector2>();
            // var tris = new List<int>();
            //
            // var meshTris = mesh.GetTriangles(subMesh);
            //
            // for (int tri = 0; tri < meshTris.Length; tri += 3) {
            //     var a = meshTris[tri + 0];
            //     var b = meshTris[tri + 1];
            //     var c = meshTris[tri + 2];
            //         
            //     verts.Add(meshVerts[a]);
            //     verts.Add(meshVerts[b]);
            //     verts.Add(meshVerts[c]);
            //         
            //     normals.Add(meshNormals[a]);
            //     normals.Add(meshNormals[b]);
            //     normals.Add(meshNormals[c]);
            //         
            //     uvs.Add(meshUvs[a]);
            //     uvs.Add(meshUvs[b]);
            //     uvs.Add(meshUvs[c]);
            //         
            //     tris.Add(tris.Count);
            //     tris.Add(tris.Count);
            //     tris.Add(tris.Count);
            // }
            //     
            // var newMesh = new Mesh {
            //     name = $"{meshName}_submesh_{submesh}",
            //     indexFormat = verts.Count > 65536 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            //     vertices = verts.ToArray(),
            //     normals = normals.ToArray(),
            //     uv = uvs.ToArray(),
            //     triangles = tris.ToArray(),
            // };

            Mesh newMesh = new Mesh();
            SubMeshDescriptor descriptor = mesh.GetSubMesh(subMesh);

            var start = descriptor.firstVertex;
            var end = descriptor.firstVertex + descriptor.vertexCount;
            newMesh.vertices = mesh.vertices[start..end];
            
            if (mesh.tangents?.Length == mesh.vertices.Length) {
                newMesh.tangents = mesh.tangents[start..end];
            }
            
            if (mesh.boneWeights?.Length == mesh.vertices.Length) {
                newMesh.boneWeights = mesh.boneWeights[start..end];
            }
            
            if (mesh.uv?.Length == mesh.vertices.Length) {
                newMesh.uv = mesh.uv[start..end];
            }
            
            if (mesh.uv2?.Length == mesh.vertices.Length) {
                newMesh.uv2 = mesh.uv2[start..end];
            }
            
            if (mesh.uv3?.Length == mesh.vertices.Length) {
                newMesh.uv3 = mesh.uv3[start..end];
            }
            
            if (mesh.uv4?.Length == mesh.vertices.Length) {
                newMesh.uv4 = mesh.uv4[start..end];
            }
            
            if (mesh.uv5?.Length == mesh.vertices.Length) {
                newMesh.uv5 = mesh.uv5[start..end];
            }
            
            if (mesh.uv6?.Length == mesh.vertices.Length) {
                newMesh.uv6 = mesh.uv6[start..end];
            }
            
            if (mesh.uv7?.Length == mesh.vertices.Length) {
                newMesh.uv7 = mesh.uv7[start..end];
            }
            
            if (mesh.uv8?.Length == mesh.vertices.Length) {
                newMesh.uv8 = mesh.uv8[start..end];
            }
            
            if (mesh.colors?.Length == mesh.vertices.Length) {
                newMesh.colors = mesh.colors[start..end];
            }
            
            if (mesh.colors32?.Length == mesh.vertices.Length) {
                newMesh.colors32 = mesh.colors32[start..end];
            }
            
            var triangles = mesh.triangles[descriptor.indexStart..(descriptor.indexStart + descriptor.indexCount)];
            for (int i = 0; i < triangles.Length; i++) {
                triangles[i] -= descriptor.firstVertex;
            }
            
            newMesh.triangles = triangles;
            
            if (mesh.normals?.Length == mesh.vertices.Length) {
                newMesh.normals = mesh.normals[start..end];
            } else {
                newMesh.RecalculateNormals();
            }
            
            // optimizing the mesh will cause the mesh to be funky
            // newMesh.Optimize();
            // newMesh.OptimizeIndexBuffers();
            
            // newMesh.RecalculateNormals();
            newMesh.RecalculateBounds();
            
            // Mesh newMesh = new Mesh();
            //
            // // Nice fast native array.
            // int[] triangles = mesh.GetTriangles(subMesh);
            // // Hashset for fast searches.
            // HashSet<int> triangleSet = new HashSet<int>(triangles);
            //
            // List<Vector3> newVertices = new List<Vector3>();
            //
            // Dictionary<int, int> oldToNewIndices = new Dictionary<int, int>();
            // int newIndex = 0;
            //
            // //do all digging into the mesh outside of the loop
            // Vector3[] oldVertices = mesh.vertices;
            // // Storing the length is not necessary...
            //
            // // Collect the vertices and uvs
            // for (int i = 0; i < oldVertices.Length; i++){
            //     if (triangleSet.Contains(i)){
            //         newVertices.Add(oldVertices[i]);
            //         oldToNewIndices.Add(i, newIndex);
            //         ++newIndex;
            //     }
            // }
            //
            // int[] newTriangles = new int[triangles.Length];
            //
            // // Collect the new triangles indecies
            // for (int i = 0; i < newTriangles.Length; i++){
            //     newTriangles[i] = oldToNewIndices[triangles[i]];
            // }
            //
            // // Assemble the new mesh with the new vertices/uv/triangles.
            // newMesh.vertices = newVertices.ToArray();
            // newMesh.triangles = newTriangles;
            // newMesh.RecalculateNormals();
            
            newMesh.name = mesh.name + $" Submesh {subMesh}";
            return newMesh;
        }

        [MenuItem("Tools/Nomnom/LC - Project Patcher/Patch Assets From Other Projects...")]
        public static void PatchAssetsFromOtherProjects() {
            var pathToFile = EditorUtility.OpenFilePanel("Select Project Information File", "", "json");
            if (string.IsNullOrEmpty(pathToFile)) {
                return;
            }

            try {
                var results = ExtractProjectInformationUtility.GetExtractedResults(pathToFile);
                GuidPatcherModule.FixGuidsWithPatcherList(results);
            } catch {
                EditorUtility.DisplayDialog("Error", "Failed to read the file!\n\nWas this the file that was exported via \"Tools/Nomnom/LC - Project Patcher/Extract Project Information\" in the original project?", "Ok");
            }
        }
    }
}
