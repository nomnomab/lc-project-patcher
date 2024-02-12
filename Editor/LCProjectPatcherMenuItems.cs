using Nomnom.LCProjectPatcher.Modules;
using UnityEditor;

namespace Nomnom.LCProjectPatcher.Editor {
    public static class LCProjectPatcherMenuItems {
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Open", priority = 1)]
        public static void ShowWindow() {
            var window = EditorWindow.GetWindow<LCProjectPatcherEditorWindow>("LC - Project Patcher");
            window.minSize = new UnityEngine.Vector2(400, 400);
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory")]
        public static void UseGameBepInExDirectory() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_game_bepinex", false);
            EditorPrefs.SetBool("nomnom.lc_project_patcher.use_game_bepinex", !v);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory", v);
            
            EditorUtility.DisplayDialog("Restart Unity",
                "You may have to restart Unity to properly unload any loaded plugins since last changing this value!",
                "Ok");
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory", true)]
        public static bool UseGameBepInExDirectory_Bool() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_game_bepinex", false);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Use Game BepInEx Directory", v);
            return true;
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Use Experimental Posterization Shader")]
        public static void UseExperimentalPosterizationShader() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_experimental_posterization_shader", false);
            EditorPrefs.SetBool("nomnom.lc_project_patcher.use_experimental_posterization_shader", !v);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Use Experimental Posterization Shader", v);
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Use Experimental Posterization Shader", true)]
        public static bool UseExperimentalPosterizationShader_Bool() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.use_experimental_posterization_shader", false);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Use Experimental Posterization Shader", v);
            return true;
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Skip Main Menu")]
        public static void WantsInstantStart() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.skip_main_menu", false);
            EditorPrefs.SetBool("nomnom.lc_project_patcher.skip_main_menu", !v);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Skip Main Menu", v);
        }
        
        [MenuItem("Tools/Nomnom/LC - Project Patcher/Skip Main Menu", true)]
        public static bool WantsInstantStart_Bool() {
            var v = EditorPrefs.GetBool("nomnom.lc_project_patcher.skip_main_menu", false);
            Menu.SetChecked("Tools/Nomnom/LC - Project Patcher/Skip Main Menu", v);
            return true;
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
