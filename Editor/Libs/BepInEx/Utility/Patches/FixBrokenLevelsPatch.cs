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
                var randomWeathers = (IList)level.GetType().GetField("randomWeathers").GetValue(level);
                var dungeonFlowTypes = (IList)level.GetType().GetField("dungeonFlowTypes").GetValue(level);
                var spawnableMapObjects = (IList)level.GetType().GetField("spawnableMapObjects").GetValue(level);
                var spawnableOutsideObjects = (IList)level.GetType().GetField("spawnableOutsideObjects").GetValue(level);
                var spawnableScrap = (IList)level.GetType().GetField("spawnableScrap").GetValue(level);
                var Enemies = (IList)level.GetType().GetField("Enemies").GetValue(level);
                var OutsideEnemies = (IList)level.GetType().GetField("OutsideEnemies").GetValue(level);
                var DaytimeEnemies = (IList)level.GetType().GetField("DaytimeEnemies").GetValue(level);
                
                // not having direct types is a pain aaaaaaaaaaaaaaaaa
                
                for (int i = 0; i < randomWeathers.Count; i++) {
                    if (randomWeathers[i] == null) {
                        randomWeathers.RemoveAt(i--);
                        Debug.Log($"Removed null random weather from {level}");
                    }
                }
                
                for (int i = 0; i < dungeonFlowTypes.Count; i++) {
                    if (dungeonFlowTypes[i] == null) {
                        dungeonFlowTypes.RemoveAt(i--);
                        Debug.Log($"Removed null dungeon flow type from {level}");
                    }
                }
                
                for (int i = 0; i < spawnableMapObjects.Count; i++) {
                    if (spawnableMapObjects[i] == null || !(GameObject)spawnableMapObjects[i].GetType().GetField("prefabToSpawn").GetValue(spawnableMapObjects[i])) {
                        spawnableMapObjects.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid spawnable map object from {level}");
                    }
                }
                
                for (int i = 0; i < spawnableOutsideObjects.Count; i++) {
                    if (spawnableOutsideObjects[i] == null || !(ScriptableObject)spawnableOutsideObjects[i].GetType().GetField("spawnableObject").GetValue(spawnableOutsideObjects[i])) {
                        spawnableOutsideObjects.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid spawnable outside object from {level}");
                    }
                }
                
                for (int i = 0; i < spawnableScrap.Count; i++) {
                    if (spawnableScrap[i] == null || !(ScriptableObject)spawnableScrap[i].GetType().GetField("spawnableItem").GetValue(spawnableScrap[i])) {
                        spawnableScrap.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid spawnable scrap from {level}");
                    }
                }
                
                for (int i = 0; i < Enemies.Count; i++) {
                    if (Enemies[i] == null || !(ScriptableObject)Enemies[i].GetType().GetField("enemyType").GetValue(Enemies[i])) {
                        Enemies.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid enemy from {level}");
                    }
                }
                
                for (int i = 0; i < OutsideEnemies.Count; i++) {
                    if (OutsideEnemies[i] == null || !(ScriptableObject)OutsideEnemies[i].GetType().GetField("enemyType").GetValue(OutsideEnemies[i])) {
                        OutsideEnemies.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid outside enemy from {level}");
                    }
                }
                
                for (int i = 0; i < DaytimeEnemies.Count; i++) {
                    if (DaytimeEnemies[i] == null || !(ScriptableObject)DaytimeEnemies[i].GetType().GetField("enemyType").GetValue(DaytimeEnemies[i])) {
                        DaytimeEnemies.RemoveAt(i--);
                        Debug.Log($"Removed null or invalid daytime enemy from {level}");
                    }
                }
            }
        }
    }
}

#endif
