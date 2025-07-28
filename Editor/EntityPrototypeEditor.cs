using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class EditorPrototypeOnBuild
    {
        private static uint nextId = 0u;
        private static HashSet<string> internalNamesLut = new();

        static EditorPrototypeOnBuild()
        {
            OnBuildUtil.RegisterAction(OnPreBuild, order: -12);
            OnBuildUtil.RegisterType<EntityPrototype>(OnBuild, order: -11);
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

        private static bool OnBuild(EntityPrototype entityPrototype)
        {
            GameObject entityPrefab = entityPrototype.EntityPrefab;
            if (entityPrefab == null
                || !EntitySystemEditorUtil.TryGetPrototypeDefinition(entityPrefab, out EntityPrototypeDefinition prototypeDefinition))
            {
                Debug.LogError($"[EntitySystem] Invalid entity prototype, missing Entity Prototype Definition.", entityPrototype);
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

            Entity defaultEntityInst = entityPrototype.DefaultEntityInst;
            if (defaultEntityInst == null
                || !PrefabUtility.IsAnyPrefabInstanceRoot(defaultEntityInst.gameObject)
                || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(defaultEntityInst) != AssetDatabase.GetAssetPath(entityPrefab))
            {
                if (defaultEntityInst != null)
                    Undo.DestroyObjectImmediate(defaultEntityInst.gameObject);
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(entityPrefab, entityPrototype.transform);
                inst.SetActive(false);
                Undo.RegisterCreatedObjectUndo(inst, "Instantiate Default Entity Inst");
                so.FindProperty("defaultEntityInst").objectReferenceValue = inst.GetComponent<Entity>();
                OnBuildUtil.MarkForRerunDueToScriptInstantiation();
            }

            so.ApplyModifiedProperties();

            if (entityPrototype.name != prototypeDefinition.name)
            {
                SerializedObject goSo = new SerializedObject(entityPrototype.gameObject);
                goSo.FindProperty("m_Name").stringValue = prototypeDefinition.name;
                goSo.ApplyModifiedProperties();
            }

            return true;
        }
    }

    [CustomEditor(typeof(EntityPrototype))]
    public class EntityPrototypeEditor : Editor
    {
        private SerializedObject so;
        private SerializedProperty entityPrefabProp;
        private GameObject entityPrefab;
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
            entityPrefabProp = so.FindProperty("entityPrefab");
            entityPrefab = (GameObject)entityPrefabProp.objectReferenceValue;
            FetchPrototypeDefinition();
        }

        private void FetchPrototypeDefinition()
        {
            if (entityPrefab == null)
                PrototypeDefinition = null;
            else
            {
                EntitySystemEditorUtil.TryGetPrototypeDefinition(entityPrefab, out var def);
                PrototypeDefinition = def;
            }
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
                return;

            so.Update();

            if (entityPrefabProp.objectReferenceValue != entityPrefab)
            {
                entityPrefab = (GameObject)entityPrefabProp.objectReferenceValue;
                FetchPrototypeDefinition();
            }

            EntityPrototypeDefinition newPrototypeDefinition = (EntityPrototypeDefinition)EditorGUILayout.ObjectField(
                new GUIContent("Entity Prototype Definition"),
                PrototypeDefinition,
                typeof(EntityPrototypeDefinition),
                allowSceneObjects: false);
            if (newPrototypeDefinition != prototypeDefinition)
            {
                entityPrefab = newPrototypeDefinition?.entityPrefab;
                PrototypeDefinition = entityPrefab == null ? null : newPrototypeDefinition;
                entityPrefabProp.objectReferenceValue = entityPrefab;

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
                definitionSo.ApplyModifiedProperties();
                EditorGUILayout.PropertyField(definitionEntityPrefabProp);
                if (definitionSo.ApplyModifiedProperties())
                {
                    entityPrefabProp.objectReferenceValue = definitionEntityPrefabProp.objectReferenceValue;
                    so.ApplyModifiedProperties();
                }
            }
        }
    }
}
