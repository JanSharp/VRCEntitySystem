using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class EntityPrototypeOnBuild
    {
        private static uint nextId = 1u;
        private static HashSet<string> internalNamesLut = new();
        private static EntitySystem entitySystem;

        static EntityPrototypeOnBuild()
        {
            OnBuildUtil.RegisterAction(OnPreBuild, order: -13);
            OnBuildUtil.RegisterType<EntitySystem>(OnFetchEntitySystem, order: -12);
            OnBuildUtil.RegisterTypeCumulative<EntityPrototype>(OnBuildCumulative, order: -11);
            OnBuildUtil.RegisterAction(OnPostBuild, order: -10);
        }

        private static bool OnPreBuild()
        {
            nextId = 1u; // Id 0 indicates invalid.
            internalNamesLut.Clear();
            return true;
        }

        private static bool OnPostBuild()
        {
            // Cleanup.
            internalNamesLut.Clear();
            return true;
        }

        private static bool OnFetchEntitySystem(EntitySystem entitySystem)
        {
            EntityPrototypeOnBuild.entitySystem = entitySystem;
            return true;
        }

        private static bool OnBuildCumulative(IEnumerable<EntityPrototype> entityPrototypes)
        {
            // Using Cumulative and just doing our own loop here due to
            // usage of OnBuildUtil.MarkForRerunDueToScriptInstantiation()
            // In the worst case scenario when not using cumulative it would trigger a rerun for every entity
            // prototype, which could quickly run into the max rerun limit that is in place to prevent
            // recursion. Plus it would just be a waste of performance rerunning so many times.
            bool result = true;
            foreach (EntityPrototype entityPrototype in entityPrototypes)
                result &= OnBuild(entityPrototype);
            return result;
        }

        private static bool OnBuild(EntityPrototype entityPrototype)
        {
            if (!Validate(entityPrototype, out EntityPrototypeDefinition prototypeDefinition))
                return false;
            SerializedObject so = new SerializedObject(entityPrototype);
            so.FindProperty("id").uintValue = nextId++;
            MirrorTheDefinition(prototypeDefinition, so);
            // TODO: Validate entity prefab. Does it have an Entity component, etc (?)
            // TODO: Must modify the prefab as the extension ids must be consistent across scenes. Cannot just modify the instances in the scene.
            EnsureEntityPrefabInstExists(entityPrototype, prototypeDefinition, so);
            EnsureDefaultEntityInstExists(entityPrototype, prototypeDefinition, so);
            so.ApplyModifiedProperties();
            EnsureGameObjectNameMatchesDefinitionName(entityPrototype, prototypeDefinition);
            return true;
        }

        private static bool Validate(
            EntityPrototype entityPrototype,
            out EntityPrototypeDefinition prototypeDefinition)
        {
            string prototypeDefinitionGuid = entityPrototype.PrototypeDefinitionGuid;
            if (prototypeDefinitionGuid == ""
                || !EntitySystemEditorUtil.TryGetPrototypeDefinition(prototypeDefinitionGuid, out prototypeDefinition))
            {
                Debug.LogError($"[EntitySystem] Invalid entity prototype, missing Entity Prototype Definition.", entityPrototype);
                prototypeDefinition = null;
                return false;
            }

            if (prototypeDefinition.entityPrefab == null)
            {
                Debug.LogError($"[EntitySystem] Invalid entity prototype, Prototype Definition is missing an Entity Prefab.", entityPrototype);
                return false;
            }

            if (internalNamesLut.Contains(prototypeDefinition.prototypeName))
            {
                Debug.LogError($"[EntitySystem] There are multiple prototypes with the internal prototype "
                    + $"name '{prototypeDefinition.prototypeName}'. A prototype can only be used once in a "
                    + $"scene, and every prototype must have a unique prototype name.", entityPrototype);
                return false;
            }
            internalNamesLut.Add(prototypeDefinition.prototypeName);

            return true;
        }

        private static void MirrorTheDefinition(
            EntityPrototypeDefinition prototypeDefinition,
            SerializedObject so)
        {
            so.FindProperty("prototypeName").stringValue = prototypeDefinition.prototypeName;
            so.FindProperty("displayName").stringValue = prototypeDefinition.displayName;
            so.FindProperty("shortDescription").stringValue = prototypeDefinition.shortDescription;
            so.FindProperty("longDescription").stringValue = prototypeDefinition.longDescription;
            so.FindProperty("defaultScale").vector3Value = prototypeDefinition.defaultScale;
            EditorUtil.SetArrayProperty(
                so.FindProperty("localExtensionIds"),
                prototypeDefinition.localExtensionIds,
                (p, v) => p.uintValue = v);
            EditorUtil.SetArrayProperty(
                so.FindProperty("extensionDataClassNames"),
                prototypeDefinition.extensionDataClassNames,
                (p, v) => p.stringValue = v);
        }

        private static void EnsureEntityPrefabInstExists(
            EntityPrototype entityPrototype,
            EntityPrototypeDefinition prototypeDefinition,
            SerializedObject so)
        {
            GameObject entityPrefab = entityPrototype.EntityPrefabInst;
            if (entityPrefab == null
                || !PrefabUtility.IsAnyPrefabInstanceRoot(entityPrefab)
                || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(entityPrefab) != AssetDatabase.GetAssetPath(prototypeDefinition.entityPrefab))
            {
                if (entityPrefab != null)
                    OnBuildUtil.UndoDestroyObjectImmediate(entityPrefab);
                entityPrefab = (GameObject)PrefabUtility.InstantiatePrefab(prototypeDefinition.entityPrefab, entitySystem.EntityPrefabInstsContainer);
                entityPrefab.SetActive(true);
                Undo.RegisterCreatedObjectUndo(entityPrefab, "Instantiate Entity Prefab");
                so.FindProperty("entityPrefabInst").objectReferenceValue = entityPrefab;
                OnBuildUtil.MarkForRerunDueToScriptInstantiation();
            }
            EnsureActiveState(entityPrefab, true); // TODO: maybe allow disabled entity prefabs.
            EnsureParent(entityPrefab.transform, entitySystem.EntityPrefabInstsContainer);
        }

        private static void EnsureDefaultEntityInstExists(
            EntityPrototype entityPrototype,
            EntityPrototypeDefinition prototypeDefinition,
            SerializedObject so)
        {
            Entity defaultEntityInst = entityPrototype.DefaultEntityInst;
            if (defaultEntityInst == null
                || !PrefabUtility.IsAnyPrefabInstanceRoot(defaultEntityInst.gameObject)
                || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(defaultEntityInst) != AssetDatabase.GetAssetPath(prototypeDefinition.entityPrefab))
            {
                if (defaultEntityInst != null)
                    OnBuildUtil.UndoDestroyObjectImmediate(defaultEntityInst.gameObject);
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prototypeDefinition.entityPrefab, entitySystem.DefaultEntityInstsContainer);
                inst.SetActive(false);
                Undo.RegisterCreatedObjectUndo(inst, "Instantiate Default Entity Inst");
                defaultEntityInst = inst.GetComponent<Entity>();
                so.FindProperty("defaultEntityInst").objectReferenceValue = defaultEntityInst;
                OnBuildUtil.MarkForRerunDueToScriptInstantiation();
            }
            EnsureActiveState(defaultEntityInst.gameObject, false); // The parent is disabled anyway, this shouldn't really matter...
            EnsureParent(defaultEntityInst.transform, entitySystem.DefaultEntityInstsContainer);
        }

        private static void EnsureGameObjectNameMatchesDefinitionName(
            EntityPrototype entityPrototype,
            EntityPrototypeDefinition prototypeDefinition)
        {
            if (entityPrototype.name != prototypeDefinition.name)
            {
                SerializedObject goSo = new SerializedObject(entityPrototype.gameObject);
                goSo.FindProperty("m_Name").stringValue = prototypeDefinition.name;
                goSo.ApplyModifiedProperties();
            }
        }

        private static void EnsureActiveState(GameObject go, bool active)
        {
            if (go.activeSelf == active)
                return;
            SerializedObject so = new SerializedObject(go);
            so.FindProperty("m_IsActive").boolValue = active;
            so.ApplyModifiedProperties();
        }

        private static void EnsureParent(Transform child, Transform parent)
        {
            if (child.parent == parent)
                return;
            Undo.SetTransformParent(child, parent, worldPositionStays: false, "Move Entity Prefab Inst");
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(EntityPrototype))]
    public class EntityPrototypeEditor : Editor
    {
        private SerializedObject so;
        private SerializedProperty prototypeDefinitionGuidProp;
        private string[] prototypeDefinitionGuids;
        private SerializedObject definitionsSo;
        private SerializedProperty prototypeNameProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty shortDescriptionProp;
        private SerializedProperty longDescriptionProp;
        private SerializedProperty definitionEntityPrefabProp;
        private EntityPrototypeDefinition shownPrototypeDefinition;

        private void OnEnable()
        {
            so = serializedObject;
            prototypeDefinitionGuidProp = so.FindProperty("prototypeDefinitionGuid");
            prototypeDefinitionGuids = GetCurrentPrototypeDefinitionGuids();
            FetchPrototypeDefinition();
        }

        private string[] GetCurrentPrototypeDefinitionGuids()
        {
            return targets.Select(t => ((EntityPrototype)t).PrototypeDefinitionGuid).ToArray();
        }

        private void FetchPrototypeDefinition()
        {
            SetPrototypeDefinitions(prototypeDefinitionGuids
                .Where(g => g != "")
                .Select(g => EntitySystemEditorUtil.TryGetPrototypeDefinition(g, out var def) ? def : null)
                .Where(d => d != null)
                .Distinct()
                .ToArray());
        }

        private void SetPrototypeDefinitions(EntityPrototypeDefinition[] prototypeDefinitions)
        {
            shownPrototypeDefinition = prototypeDefinitions.FirstOrDefault();
            definitionsSo = prototypeDefinitions.Length == 0
                ? null
                : new SerializedObject(prototypeDefinitions);
            prototypeNameProp = definitionsSo == null ? null : definitionsSo.FindProperty("prototypeName");
            displayNameProp = definitionsSo == null ? null : definitionsSo.FindProperty("displayName");
            shortDescriptionProp = definitionsSo == null ? null : definitionsSo.FindProperty("shortDescription");
            longDescriptionProp = definitionsSo == null ? null : definitionsSo.FindProperty("longDescription");
            definitionEntityPrefabProp = definitionsSo == null ? null : definitionsSo.FindProperty("entityPrefab");
        }

        private bool CompareStringArrays(string[] left, string[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;
            return true;
        }

        private void SetPrototypeDefinition(EntityPrototypeDefinition prototypeDefinition)
        {
            string prototypeDefinitionGuid = EntitySystemEditorUtil.GetAssetGuid(prototypeDefinition);
            SetPrototypeDefinitions(prototypeDefinitionGuid == ""
                ? new EntityPrototypeDefinition[0]
                : new EntityPrototypeDefinition[1] { prototypeDefinition });
            prototypeDefinitionGuidProp.stringValue = prototypeDefinitionGuid;

            if (shownPrototypeDefinition == null)
                return;
            SerializedObject goSo = new SerializedObject(targets.Select(t => ((EntityPrototype)t).gameObject).ToArray());
            goSo.FindProperty("m_Name").stringValue = shownPrototypeDefinition.name;
            goSo.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
                return;

            so.Update();

            string[] newEntityPrefabGuids = GetCurrentPrototypeDefinitionGuids();
            if (!CompareStringArrays(prototypeDefinitionGuids, newEntityPrefabGuids))
            {
                prototypeDefinitionGuids = newEntityPrefabGuids;
                FetchPrototypeDefinition();
            }

            EditorGUI.showMixedValue = prototypeDefinitionGuidProp.hasMultipleDifferentValues;
            EntityPrototypeDefinition newPrototypeDefinition = (EntityPrototypeDefinition)EditorGUILayout.ObjectField(
                new GUIContent("Entity Prototype Definition"),
                shownPrototypeDefinition,
                typeof(EntityPrototypeDefinition),
                allowSceneObjects: false);
            EditorGUI.showMixedValue = false;
            if (newPrototypeDefinition != shownPrototypeDefinition)
                SetPrototypeDefinition(newPrototypeDefinition);

            so.ApplyModifiedProperties();

            if (definitionsSo == null)
                return;

            EditorGUILayout.Space();
            if (prototypeDefinitionGuids.Any(g => g == ""))
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    GUILayout.Label("Some of the selected Entity Prototypes do not have a definition set.", EditorStyles.wordWrappedLabel);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Inlined Prototype Definition", EditorStyles.boldLabel);
                definitionsSo.Update();
                EditorGUILayout.PropertyField(prototypeNameProp);
                EditorGUILayout.PropertyField(displayNameProp);
                EditorGUILayout.PropertyField(shortDescriptionProp);
                EditorGUILayout.PropertyField(longDescriptionProp);
                EditorGUILayout.PropertyField(definitionEntityPrefabProp);
                definitionsSo.ApplyModifiedProperties();
            }
        }
    }
}
