using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
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
            OnBuildUtil.RegisterType<EntitySystem>(OnBuild, order: 0);
        }

        private static bool OnPrototypesBuild(IEnumerable<EntityPrototype> prototypes)
        {
            EntitySystemOnBuild.prototypes = prototypes.ToList();
            return true;
        }

        private static bool OnBuild(EntitySystem entitySystem)
        {
            SerializedObject so = new SerializedObject(entitySystem);

            List<string> rawExtensionMethodNamesLut = prototypes
                .SelectMany(p => p.DefaultEntityInst.extensions)
                .Select(e =>
                {
                    if (EntitySystemEditorUtil.IsEntityExtension(e.GetType(), out System.Type extensionDataType))
                        return extensionDataType;
                    Debug.LogError($"[EntitySystem] Impossible, an entity prototype's default entity has "
                        + $"an extension that is not in the extension types lut.", e);
                    return null;
                })
                .Where(t => t != null)
                .Distinct()
                .OrderBy(t => t.Name)
                .Select(t => (
                    className: t.Name,
                    iaHandlers: EditorUtil.GetMethodsIncludingBase(t, BindingFlags.Instance | BindingFlags.Public, typeof(UdonSharpBehaviour))
                        .Where(m => m.GetCustomAttribute<EntityExtensionDataInputActionAttribute>() != null)
                        .Select(m => m.Name)
                        .OrderBy(n => n)
                        .ToList()
                    ))
                .Aggregate(new List<string>(), (list, e) =>
                {
                    list.Add(e.className);
                    list.Add(e.iaHandlers.Count.ToString("d"));
                    list.AddRange(e.iaHandlers);
                    return list;
                });
            EditorUtil.SetArrayProperty(
                so.FindProperty("rawExtensionMethodNamesLut"),
                rawExtensionMethodNamesLut,
                (p, v) => p.stringValue = v);

            EditorUtil.SetArrayProperty(
                so.FindProperty("entityPrototypes"),
                prototypes,
                (p, v) => p.objectReferenceValue = v);

            Dictionary<string, EntityPrototype> prefabAssetPathToPrototypeLut
                = prototypes.ToDictionary(p => AssetDatabase.GetAssetPath(p.EntityPrefab), p => p);

            bool invalid = false;
            List<Entity> preInstantiatedEntityInstances = EditorUtil.EnumerateArrayProperty(so.FindProperty("preInstantiatedEntityInstances"))
                .Select(p => (Entity)p.objectReferenceValue)
                .ToList();

            EntityPrototype[] preInstantiatedEntityInstancePrototypes = new EntityPrototype[preInstantiatedEntityInstances.Count];
            for (int i = 0; i < preInstantiatedEntityInstances.Count; i++)
            {
                Entity entity = preInstantiatedEntityInstances[i];
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(entity.gameObject))
                {
                    Debug.LogError($"[EntitySystem] Invalid pre instantiated entity: '{entity.name}'. "
                        + $"Every pre instantiated entity must be a prefab instance. Also note that "
                        + $"said prefab must be referenced by an {nameof(EntityPrototypeDefinition)} asset "
                        + $"and said prototype definition must be referenced by an {nameof(EntityPrototype)} "
                        + $"in the scene", entity);
                    invalid = true;
                    continue;
                }
                string prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(entity);
                if (!prefabAssetPathToPrototypeLut.TryGetValue(prefabAssetPath, out EntityPrototype prototype))
                {
                    if (EntitySystemEditorUtil.TryGetPrototypeDefinition(AssetDatabase.AssetPathToGUID(prefabAssetPath), out var prototypeDefinition))
                        Debug.LogError($"[EntitySystem] Invalid pre instantiated entity: '{entity.name}'. "
                            + $"The '{prototypeDefinition.name}' (prototype name '{prototypeDefinition.prototypeName}') "
                            + $"can only be used in a scene where there is an {nameof(EntityPrototype)} which "
                            + $"is referencing the '{prototypeDefinition.name}' "
                            + $"{nameof(EntityPrototypeDefinition)} asset.", entity); // TODO: Maybe mention inspector buttons that resolve this.
                    else
                        Debug.LogError($"[EntitySystem] Invalid pre instantiated entity: '{entity.name}'. "
                            + $"Every pre instantiated entity must be a prefab instance where "
                            + $"said prefab is referenced by an {nameof(EntityPrototypeDefinition)} asset "
                            + $"and said prototype definition is referenced by an {nameof(EntityPrototype)} "
                            + $"in the scene.", entity);
                    invalid = true;
                    continue;
                }
                if (invalid)
                    continue;
                preInstantiatedEntityInstancePrototypes[i] = prototype;
            }
            if (invalid)
                return false;

            EditorUtil.SetArrayProperty(
                so.FindProperty("preInstantiatedEntityInstancePrototypes"),
                preInstantiatedEntityInstancePrototypes,
                (p, v) => p.objectReferenceValue = v);

            prototypes = null; // Cleanup.
            so.ApplyModifiedProperties();
            return true;
        }
    }
}
