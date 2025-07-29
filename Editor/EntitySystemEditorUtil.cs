using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    [DefaultExecutionOrder(-10)]
    public static class EntitySystemEditorUtil
    {
        private class ExtensionTypePair
        {
            public System.Type extensionType;
            public System.Type extensionDataType;

            public ExtensionTypePair(System.Type extensionType, System.Type extensionDataType)
            {
                this.extensionType = extensionType;
                this.extensionDataType = extensionDataType;
            }
        }

        private static Dictionary<System.Type, ExtensionTypePair> extensionTypeToPairLut = new();
        private static Dictionary<System.Type, ExtensionTypePair> extensionDataTypeToPairLut = new();

        private static bool hasInvalidAssociationAttributes = false;
        public static bool HasInvalidAssociationAttributes => hasInvalidAssociationAttributes;

        private static Dictionary<GameObject, EntityPrototypeDefinition> prefabToPrototypeDefLut = new();

        static EntitySystemEditorUtil()
        {
            if (!FindAndValidateEntityDataAssociations())
                OnBuildUtil.RegisterAction(OnInvalidEntityExtensionAssociationsBuild, order: -10000);
        }

        private static bool OnInvalidEntityExtensionAssociationsBuild()
        {
            FindAndValidateEntityDataAssociations();
            return false;
        }

        private static bool FindAndValidateEntityDataAssociations()
        {
            extensionTypeToPairLut.Clear();
            extensionDataTypeToPairLut.Clear();
            hasInvalidAssociationAttributes = false;

            List<System.Type> extensionDataTypes = new List<System.Type>();
            foreach (System.Type ubType in OnAssemblyLoadUtil.AllUdonSharpBehaviourTypes)
            {
                if (ubType.IsAbstract)
                    continue;
                var attr = ubType.GetCustomAttribute<AssociatedEntityExtensionDataAttribute>(inherit: true);
                if (attr == null)
                {
                    if (EditorUtil.DerivesFrom(ubType, typeof(EntityExtension)))
                    {
                        Debug.LogError($"[EntitySystem] The '{ubType.Name}' class is missing the "
                            + $"[AssociatedEntityExtensionData] attribute. Every {nameof(EntityExtension)} "
                            + $"must have an associated {nameof(EntityExtensionData)}.");
                        hasInvalidAssociationAttributes = true;
                    }
                    if (EditorUtil.DerivesFrom(ubType, typeof(EntityExtensionData)))
                        extensionDataTypes.Add(ubType);
                    continue;
                }
                if (attr.AssociatedDataType == null)
                {
                    Debug.LogError($"[EntitySystem] Missing associated data type for "
                        + $"[AssociatedEntityExtensionData] attribute for class '{ubType.Name}'.");
                    hasInvalidAssociationAttributes = true;
                    continue;
                }
                ProcessExtensionTypePair(ubType, attr.AssociatedDataType);
            }

            foreach (System.Type extensionDataType in extensionDataTypes)
            {
                if (extensionDataTypeToPairLut.ContainsKey(extensionDataType))
                    continue;
                Debug.LogError($"[EntitySystem] The '{extensionDataType.Name}' class is missing an "
                    + $"{nameof(EntityExtension)} referencing it through an [AssociatedEntityExtensionData] attribute. "
                    + $"Every {nameof(EntityExtensionData)} must have an associated {nameof(EntityExtension)}.");
                hasInvalidAssociationAttributes = true;
            }

            return !hasInvalidAssociationAttributes;
        }

        private static void ProcessExtensionTypePair(System.Type extensionType, System.Type extensionDataType)
        {
            if (!EditorUtil.DerivesFrom(extensionType, typeof(EntityExtension)))
            {
                Debug.LogError($"[EntitySystem] Classes with the [AssociatedEntityExtensionData] attribute "
                    + $"must derive from {nameof(EntityExtension)}, however the '{extensionType.Name}' class does not.");
                hasInvalidAssociationAttributes = true;
                return;
            }
            if (!EditorUtil.DerivesFrom(extensionDataType, typeof(EntityExtensionData)))
            {
                Debug.LogError($"[EntitySystem] Classes that are associated with entity extensions through the "
                    + $"[AssociatedEntityExtensionData] attribute must derive from {nameof(EntityExtensionData)}, "
                    + $"however the '{extensionDataType.Name}' class does not. The attribute is defined on the "
                    + $"'{extensionType.Name}' class.");
                hasInvalidAssociationAttributes = true;
                return;
            }
            if (extensionDataTypeToPairLut.TryGetValue(extensionDataType, out var existingPair))
            {
                Debug.LogError($"[EntitySystem] Multiple entity extension classes are attempting to use the "
                    + $"'{extensionDataType.Name}' class through the [AssociatedEntityExtensionData] attribute. "
                    + $"Entity extension class names: '{existingPair.extensionType.Name}' and '{extensionType.Name}'.");
                hasInvalidAssociationAttributes = true;
                return;
            }
            ExtensionTypePair pair = new ExtensionTypePair(extensionType, extensionDataType);
            extensionTypeToPairLut.Add(extensionType, pair);
            extensionDataTypeToPairLut.Add(extensionDataType, pair);
        }

        public static bool IsEntityExtension(System.Type ubType, out System.Type extensionDataType)
        {
            if (extensionTypeToPairLut.TryGetValue(ubType, out var pair))
            {
                extensionDataType = pair.extensionDataType;
                return true;
            }
            extensionDataType = null;
            return false;
        }

        public static bool IsEntityExtensionData(System.Type ubType, out System.Type extensionType)
        {
            if (extensionDataTypeToPairLut.TryGetValue(ubType, out var pair))
            {
                extensionType = pair.extensionType;
                return true;
            }
            extensionType = null;
            return false;
        }

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
