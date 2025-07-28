using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class EntitySystemOnBuild
    {
        private static List<EntityPrototype> prototypes;

        static EntitySystemOnBuild()
        {
            OnBuildUtil.RegisterTypeCumulative<EntityPrototype>(OnPrototypesBuild, order: -1);
            OnBuildUtil.RegisterType<EntitySystem>(OnBuild);
        }

        private static bool OnPrototypesBuild(IEnumerable<EntityPrototype> prototypes)
        {
            EntitySystemOnBuild.prototypes = prototypes.ToList();
            return true;
        }

        private static bool OnBuild(EntitySystem entitySystem)
        {
            SerializedObject so = new SerializedObject(entitySystem);

            EditorUtil.SetArrayProperty(
                so.FindProperty("entityPrototypes"),
                prototypes,
                (p, v) => p.objectReferenceValue = v);
            prototypes = null; // Cleanup.

            so.ApplyModifiedProperties();
            return true;
        }
    }

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
            // TODO: Somehow error when multiple prototypes are using the same prefab.
            string[] guids = AssetDatabase.FindAssets("t:EntityPrototypeDefinition");
            prefabToPrototypeDefLut = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<EntityPrototypeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p.entityPrefab != null)
                .ToDictionary(p => p.entityPrefab, p => p);
        }
    }
}
