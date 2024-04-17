#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace Patches {
    public static class FixBrokenLevelsPatch {
        public static MethodBase TargetMethod() {
            return AccessTools.Method("PreInitSceneScript:Awake");
        }

        public static void Postfix() {
            // var levels = Resources.FindObjectsOfTypeAll(AccessTools.TypeByName("SelectableLevel"));
            var levels = AssetDatabase.FindAssets("t:SelectableLevel");
            foreach (var level in levels.Select(x => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(x)))) {
                // Debug.Log($"Fixing {level}");
                var randomWeathers = new BoxedArray(level.GetType().GetField("randomWeathers").GetValue(level)); // array
                var dungeonFlowTypes = new BoxedArray(level.GetType().GetField("dungeonFlowTypes").GetValue(level)); // array
                var spawnableMapObjects = new BoxedArray(level.GetType().GetField("spawnableMapObjects").GetValue(level)); // array
                var spawnableOutsideObjects = new BoxedArray(level.GetType().GetField("spawnableOutsideObjects").GetValue(level)); // array
                var spawnableScrap = (IList)level.GetType().GetField("spawnableScrap").GetValue(level); // list
                var Enemies = (IList)level.GetType().GetField("Enemies").GetValue(level); // list
                var OutsideEnemies = (IList)level.GetType().GetField("OutsideEnemies").GetValue(level); // list
                var DaytimeEnemies = (IList)level.GetType().GetField("DaytimeEnemies").GetValue(level); // list
                
                // not having direct types is a pain aaaaaaaaaaaaaaaaa

                var modified = false;
                for (int i = 0; i < randomWeathers.Length; i++) {
                    if (randomWeathers[i] == null) {
                        randomWeathers.RemoveAt(i--);
                        modified = true;
                        Debug.Log($"Removed null random weather from {level}");
                    }
                }

                if (modified) {
                    level.GetType().GetField("randomWeathers").SetValue(level, randomWeathers.GetProperArray(level.GetType().GetField("randomWeathers").FieldType.GetElementType()));
                }

                modified = false;
                for (int i = 0; i < dungeonFlowTypes.Length; i++) {
                    if (dungeonFlowTypes[i] == null) {
                        dungeonFlowTypes.RemoveAt(i--);
                        Debug.Log($"Removed null dungeon flow type from {level}");
                        modified = true;
                    }
                }

                if (modified) {
                    level.GetType().GetField("dungeonFlowTypes").SetValue(level, dungeonFlowTypes.GetProperArray(level.GetType().GetField("dungeonFlowTypes").FieldType.GetElementType()));
                }

                modified = false;
                for (int i = 0; i < spawnableMapObjects.Length; i++) {
                    if (spawnableMapObjects[i] == null || !(GameObject)spawnableMapObjects[i].GetType().GetField("prefabToSpawn").GetValue(spawnableMapObjects[i])) {
                        spawnableMapObjects.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid spawnable map object from {level}");
                        modified = true;
                    }
                }
                
                if (modified) {
                    level.GetType().GetField("spawnableMapObjects").SetValue(level, spawnableMapObjects.GetProperArray(level.GetType().GetField("spawnableMapObjects").FieldType.GetElementType()));    
                }
                
                modified = false;
                for (int i = 0; i < spawnableOutsideObjects.Length; i++) {
                    if (spawnableOutsideObjects[i] == null || !(ScriptableObject)spawnableOutsideObjects[i].GetType().GetField("spawnableObject").GetValue(spawnableOutsideObjects[i])) {
                        spawnableOutsideObjects.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid spawnable outside object from {level}");
                        modified = true;
                    }
                }
                
                if (modified) {
                    level.GetType().GetField("spawnableOutsideObjects").SetValue(level, spawnableOutsideObjects.GetProperArray(level.GetType().GetField("spawnableOutsideObjects").FieldType.GetElementType()));    
                }
                
                modified = false;
                for (int i = 0; i < spawnableScrap.Count; i++) {
                    if (spawnableScrap[i] == null || !(ScriptableObject)spawnableScrap[i].GetType().GetField("spawnableItem").GetValue(spawnableScrap[i])) {
                        spawnableScrap.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid spawnable scrap from {level}");
                        modified = true;
                    }
                }
                
                if (modified) {
                    level.GetType().GetField("spawnableScrap").SetValue(level, spawnableScrap);    
                }
                
                modified = false;
                for (int i = 0; i < Enemies.Count; i++) {
                    if (Enemies[i] == null || !(ScriptableObject)Enemies[i].GetType().GetField("enemyType").GetValue(Enemies[i])) {
                        Enemies.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid enemy from {level}");
                        modified = true;
                    }
                }
                
                if (modified) {
                    level.GetType().GetField("Enemies").SetValue(level, Enemies);    
                }
                
                modified = false;
                for (int i = 0; i < OutsideEnemies.Count; i++) {
                    if (OutsideEnemies[i] == null || !(ScriptableObject)OutsideEnemies[i].GetType().GetField("enemyType").GetValue(OutsideEnemies[i])) {
                        OutsideEnemies.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid outside enemy from {level}");
                        modified = true;
                    }
                }
                
                if (modified) {
                    level.GetType().GetField("OutsideEnemies").SetValue(level, OutsideEnemies);    
                }
                
                modified = false;
                for (int i = 0; i < DaytimeEnemies.Count; i++) {
                    if (DaytimeEnemies[i] == null || !(ScriptableObject)DaytimeEnemies[i].GetType().GetField("enemyType").GetValue(DaytimeEnemies[i])) {
                        DaytimeEnemies.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid daytime enemy from {level}");
                        modified = true;
                    }
                }
                
                if (modified) {
                    level.GetType().GetField("DaytimeEnemies").SetValue(level, DaytimeEnemies);    
                }
            }
        }
    }
}

#endif
