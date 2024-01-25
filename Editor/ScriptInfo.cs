using System;
using System.Linq;
using UnityEngine;

namespace Nomnom.LCProjectPatcher {
    public readonly struct ScriptInfo {
        private readonly static Type[] AllTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => !x.IsGenericType && !x.IsAbstract && !x.IsInterface)
            .Where(x => typeof(Component).IsAssignableFrom(x) || typeof(ScriptableObject).IsAssignableFrom(x) || typeof(MonoBehaviour).IsAssignableFrom(x) || typeof(UnityEngine.Object).IsAssignableFrom(x))
            .ToArray();

        public readonly string? namespaceName;
        public readonly string fileName;
        public readonly string guid;

        public ScriptInfo(string? namespaceName, string fileName, string guid) {
            this.namespaceName = namespaceName?.Trim();
            this.fileName = fileName.Trim();
            this.guid = guid.Trim();
        }

        public bool TryGetType(out Type type) {
            var typeString = ToString();
            var matchingTypes = AllTypes.Where(x => x.FullName == typeString).ToArray();

            type = null;

            var count = matchingTypes.Count();
            if (count == 0) {
                return false;
            }

            if (count > 1) {
                Debug.LogWarning($"Found {count} types matching {typeString}");
                return false;
            }

            type = matchingTypes.First();
            return true;
        }

        public override string ToString() {
            return namespaceName is null ? fileName : $"{namespaceName}.{fileName}";
        }
    }
}
