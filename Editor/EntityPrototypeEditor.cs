using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class EntityPrototypeOnBuild
    {
        private static uint nextId = 0u;
        private static HashSet<string> internalNamesLut = new();
        private static EntitySystem entitySystem;

        static EntityPrototypeOnBuild()
        {
            OnBuildUtil.RegisterAction(OnPreBuild, order: -13);
            OnBuildUtil.RegisterType<EntitySystem>(OnFetchEntitySystem, order: -12);
            OnBuildUtil.RegisterType<EntityPrototype>(OnBuild, order: -11); // TODO: change this to be cumulative due to MarkForRerunDueToScriptInstantiation
            OnBuildUtil.RegisterAction(OnPostBuild, order: -10);
        }

        private static bool OnPreBuild()
        {
            nextId = 0u;
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

        private static bool OnBuild(EntityPrototype entityPrototype)
        {
            string prototypeDefinitionGuid = entityPrototype.PrototypeDefinitionGuid;
            if (prototypeDefinitionGuid == ""
                || !EntitySystemEditorUtil.TryGetPrototypeDefinition(prototypeDefinitionGuid, out EntityPrototypeDefinition prototypeDefinition))
            {
                Debug.LogError($"[EntitySystem] Invalid entity prototype, missing Entity Prototype Definition.", entityPrototype);
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

            SerializedObject so = new SerializedObject(entityPrototype);

            so.FindProperty("id").uintValue = nextId++;

            // Mirroring.
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

            // TODO: Validate entity prefab. Does it have an Entity component, etc (?)

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

            so.ApplyModifiedProperties();

            if (entityPrototype.name != prototypeDefinition.name)
            {
                SerializedObject goSo = new SerializedObject(entityPrototype.gameObject);
                goSo.FindProperty("m_Name").stringValue = prototypeDefinition.name;
                goSo.ApplyModifiedProperties();
            }

            return true;
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

    // TODO: add the multi edit attribute and when multiple are selected instead of inlining the definition
    // properties, show a button which when pressed selects all the definitions for the currently selected
    // entity prototypes.
    [CustomEditor(typeof(EntityPrototype))]
    public class EntityPrototypeEditor : Editor
    {
        private SerializedObject so;
        private SerializedProperty prototypeDefinitionGuidProp;
        private string entityPrefabGuid;
        private SerializedObject definitionSo;
        private SerializedProperty prototypeNameProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty shortDescriptionProp;
        private SerializedProperty longDescriptionProp;
        private SerializedProperty definitionEntityPrefabProp;
        private EntityPrototypeDefinition prototypeDefinition;
        private EntityPrototypeDefinition PrototypeDefinition
        {
            get => prototypeDefinition;
            set
            {
                prototypeDefinition = value;
                definitionSo = value == null ? null : new SerializedObject(value);
                if (definitionSo == null)
                    return;
                prototypeNameProp = definitionSo.FindProperty("prototypeName");
                displayNameProp = definitionSo.FindProperty("displayName");
                shortDescriptionProp = definitionSo.FindProperty("shortDescription");
                longDescriptionProp = definitionSo.FindProperty("longDescription");
                definitionEntityPrefabProp = definitionSo.FindProperty("entityPrefab");
            }
        }

        private void OnEnable()
        {
            so = serializedObject;
            prototypeDefinitionGuidProp = so.FindProperty("prototypeDefinitionGuid");
            entityPrefabGuid = prototypeDefinitionGuidProp.stringValue;
            FetchPrototypeDefinition();
        }

        private void FetchPrototypeDefinition()
        {
            if (entityPrefabGuid == "")
                PrototypeDefinition = null;
            else
            {
                EntitySystemEditorUtil.TryGetPrototypeDefinition(entityPrefabGuid, out var def);
                PrototypeDefinition = def;
            }
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
                return;

            so.Update();

            if (prototypeDefinitionGuidProp.stringValue != entityPrefabGuid)
            {
                entityPrefabGuid = prototypeDefinitionGuidProp.stringValue;
                FetchPrototypeDefinition();
            }

            EntityPrototypeDefinition newPrototypeDefinition = (EntityPrototypeDefinition)EditorGUILayout.ObjectField(
                new GUIContent("Entity Prototype Definition"),
                PrototypeDefinition,
                typeof(EntityPrototypeDefinition),
                allowSceneObjects: false);
            if (newPrototypeDefinition != prototypeDefinition)
            {
                entityPrefabGuid = EntitySystemEditorUtil.GetAssetGuid(newPrototypeDefinition);
                PrototypeDefinition = entityPrefabGuid == "" ? null : newPrototypeDefinition;
                prototypeDefinitionGuidProp.stringValue = entityPrefabGuid;

                SerializedObject goSo = new SerializedObject(((EntityPrototype)target).gameObject);
                goSo.FindProperty("m_Name").stringValue = prototypeDefinition.name;
                goSo.ApplyModifiedProperties();
            }

            so.ApplyModifiedProperties();

            if (definitionSo == null)
                return;

            EditorGUILayout.Space();
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Inlining definition properties here for convenience. Note that the definition "
                    + "inspector does support multi editing while this here does not.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space();

                definitionSo.Update();
                EditorGUILayout.PropertyField(prototypeNameProp);
                EditorGUILayout.PropertyField(displayNameProp);
                EditorGUILayout.PropertyField(shortDescriptionProp);
                EditorGUILayout.PropertyField(longDescriptionProp);
                EditorGUILayout.PropertyField(definitionEntityPrefabProp);
                definitionSo.ApplyModifiedProperties();
            }
        }
    }
}
