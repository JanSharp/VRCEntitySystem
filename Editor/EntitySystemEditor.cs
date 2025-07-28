using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    public static class EntitySystemEditorUtil
    {
        private static Dictionary<GameObject, EntityPrototypeDefinition> prefabToPrototypeDefLut = new();

        public static bool TryGetPrototypeDefinition(GameObject prefabAsset, out EntityPrototypeDefinition prototypeDefinition)
        {
            if (prefabToPrototypeDefLut.TryGetValue(prefabAsset, out prototypeDefinition)
                && prototypeDefinition != null && prototypeDefinition.entityPrefab == prefabAsset)
            {
                return true;
            }
            FindAllPrototypeDefinitions();
            return prefabToPrototypeDefLut.TryGetValue(prefabAsset, out prototypeDefinition);
        }

        public static void FindAllPrototypeDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:EntityPrototypeDefinition");
            prefabToPrototypeDefLut = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<EntityPrototypeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p.entityPrefab != null)
                .ToDictionary(p => p.entityPrefab, p => p);
        }
    }
}
