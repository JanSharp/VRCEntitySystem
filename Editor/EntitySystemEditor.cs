using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
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

        private static System.Type GetAssociatedEntityExtensionDataType(EntityExtension extension)
        {
            if (EntitySystemEditorUtil.IsEntityExtension(extension.GetType(), out System.Type extensionDataType))
                return extensionDataType;
            Debug.LogError($"[EntitySystem] Impossible, an entity prototype's default entity has "
                + $"an extension that is not in the extension types lut.", extension);
            return null;
        }

        private static bool OnBuild(EntitySystem entitySystem)
        {
            SerializedObject so = new SerializedObject(entitySystem);

            List<string> rawExtensionMethodNamesLut = prototypes
                .SelectMany(p => p.DefaultEntityInst.extensions)
                .Select(GetAssociatedEntityExtensionDataType)
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

            GeneratePreInstantiatedEntityData(so, preInstantiatedEntityInstancePrototypes);

            prototypes = null; // Cleanup.
            so.ApplyModifiedProperties();
            return true;
        }

        private static void GeneratePreInstantiatedEntityData(SerializedObject so, EntityPrototype[] preInstantiatedEntityInstancePrototypes)
        {
            Transform container = (Transform)so.FindProperty("preInstantiatedEntityDataContainer").objectReferenceValue;
            List<Transform> toDestroy = new();
            List<WannaBeClass> existing = new();
            foreach (Transform child in container)
            {
                WannaBeClass wannaBeClass = child.GetComponent<WannaBeClass>();
                if (wannaBeClass == null)
                    toDestroy.Add(child);
                else
                    existing.Add(wannaBeClass);
            }
            foreach (Transform child in toDestroy)
                OnBuildUtil.UndoDestroyObjectImmediate(child.gameObject);
            Dictionary<System.Type, List<WannaBeClass>> existingByType = existing
                .GroupBy(c => c.GetType())
                .ToDictionary(g => g.Key, g => g.ToList());

            List<System.Type> requiredWannaBeTypes = new();
            Dictionary<EntityPrototype, List<System.Type>> extensionDataTypesByPrototype
                = preInstantiatedEntityInstancePrototypes
                    .Distinct()
                    .ToDictionary(
                        p => p,
                        p => p.DefaultEntityInst.extensions
                            .Select(GetAssociatedEntityExtensionDataType)
                            .Where(t => t != null)
                            .ToList());
            // We are building slopes instead of mountains now!
            foreach (EntityPrototype prototype in preInstantiatedEntityInstancePrototypes)
            {
                requiredWannaBeTypes.Add(typeof(EntityData));
                requiredWannaBeTypes.AddRange(extensionDataTypesByPrototype[prototype]);
            }

            int index = 0;
            HashSet<System.Type> requiredClassTypes = new();
            List<EntityData> entityDataInsts = null;
            foreach (var group in requiredWannaBeTypes
                .GroupBy(t => t)
                .OrderBy(g => g.Key.Name))
            {
                requiredClassTypes.Add(group.Key);
                int requiredCount = group.Count();
                if (existingByType.TryGetValue(group.Key, out List<WannaBeClass> insts))
                {
                    int canUseCount = System.Math.Min(requiredCount, insts.Count);
                    requiredCount -= canUseCount;
                    WannaBeClass[] reused = new WannaBeClass[canUseCount];
                    for (int i = 0; i < canUseCount; i++)
                    {
                        WannaBeClass inst = insts[i];
                        Undo.SetSiblingIndex(inst.transform, index++, "Generate Pre Instantiated Entity Data");
                        reused[i] = inst;
                    }
                    SerializedObject instsSo = new SerializedObject(reused);
                    instsSo.FindProperty("m_Name").stringValue = group.Key.Name;
                    instsSo.ApplyModifiedProperties();
                    for (int i = canUseCount; i < insts.Count; i++)
                        OnBuildUtil.UndoDestroyObjectImmediate(insts[i].gameObject);
                    insts.RemoveRange(canUseCount, insts.Count - canUseCount);
                }
                else
                {
                    insts = new();
                    existingByType.Add(group.Key, insts);
                }

                for (int i = 0; i < requiredCount; i++)
                {
                    GameObject inst = new GameObject(group.Key.Name);
                    Undo.RegisterCreatedObjectUndo(inst, "Generate Pre Instantiated Entity Data");
                    inst.transform.SetParent(container, worldPositionStays: false);
                    inst.transform.SetSiblingIndex(index++);
                    insts.Add((WannaBeClass)UdonSharpUndo.AddComponent(inst, group.Key));
                    OnBuildUtil.MarkForRerunDueToScriptInstantiation();
                }

                if (group.Key == typeof(EntityData))
                    entityDataInsts = insts.Cast<EntityData>().ToList();
            }

            foreach (var kvp in existingByType)
            {
                if (requiredClassTypes.Contains(kvp.Key))
                    continue;
                foreach (WannaBeClass inst in kvp.Value)
                    OnBuildUtil.UndoDestroyObjectImmediate(inst.gameObject);
            }

            for (int i = 0; i < entityDataInsts.Count; i++)
            {
                EntityData entityData = entityDataInsts[i];
                EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[i];
                List<System.Type> extensionDataTypes = extensionDataTypesByPrototype[prototype];
                EntityExtensionData[] allExtensionData = new EntityExtensionData[extensionDataTypes.Count];
                for (int j = 0; j < extensionDataTypes.Count; j++)
                {
                    List<WannaBeClass> insts = existingByType[extensionDataTypes[j]];
                    allExtensionData[j] = (EntityExtensionData)insts[^1];
                    insts.RemoveAt(insts.Count - 1);
                }
                SerializedObject entityDataSo = new SerializedObject(entityData);
                EditorUtil.SetArrayProperty(
                    entityDataSo.FindProperty("allExtensionData"),
                    allExtensionData,
                    (p, v) => p.objectReferenceValue = v);
                entityDataSo.ApplyModifiedProperties();
            }

            EditorUtil.SetArrayProperty(
                so.FindProperty("preInstantiatedEntityData"),
                entityDataInsts,
                (p, v) => p.objectReferenceValue = v);
        }
    }

    [CustomEditor(typeof(EntitySystem))]
    public class EntitySystemEditor : Editor
    {
#if EntitySystemDebug
        private SerializedObject so;
        private SerializedProperty preInstantiatedEntityDataContainerProp;

        private void OnEnable()
        {
            so = serializedObject;
            preInstantiatedEntityDataContainerProp = so.FindProperty("preInstantiatedEntityDataContainer");
        }
#endif

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
                return;
#if EntitySystemDebug
            so.Update();
            EditorGUILayout.PropertyField(preInstantiatedEntityDataContainerProp);
            so.ApplyModifiedProperties();
#endif
        }
    }
}
