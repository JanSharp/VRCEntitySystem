using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class EditorPrototypeOnBuild
    {
        static EditorPrototypeOnBuild()
        {
            OnBuildUtil.RegisterType<EntityPrototype>(OnBuild);
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

            SerializedObject so = new SerializedObject(entityPrototype);

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

            return true;
        }
    }

    [CustomEditor(typeof(EntityPrototype))]
    public class EntityPrototypeEditor : Editor
    {
        private SerializedObject so;
        private SerializedProperty idProp;
        private SerializedProperty entityPrefabProp;
        private GameObject entityPrefab;
        private EntityPrototypeDefinition prototypeDefinition;

        private void OnEnable()
        {
            so = serializedObject;
            idProp = so.FindProperty("id");
            entityPrefabProp = so.FindProperty("entityPrefab");
            entityPrefab = (GameObject)entityPrefabProp.objectReferenceValue;
            FetchPrototypeDefinition();
        }

        private void FetchPrototypeDefinition()
        {
            if (entityPrefab == null)
                prototypeDefinition = null;
            else
                EntitySystemEditorUtil.TryGetPrototypeDefinition(entityPrefab, out prototypeDefinition);
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
                return;

            so.Update();

            // FIXME: Temp until id editor scripting exists.
            EditorGUILayout.PropertyField(idProp);

            if (entityPrefabProp.objectReferenceValue != entityPrefab)
            {
                entityPrefab = (GameObject)entityPrefabProp.objectReferenceValue;
                FetchPrototypeDefinition();
            }

            EntityPrototypeDefinition newPrototypeDefinition = (EntityPrototypeDefinition)EditorGUILayout.ObjectField(
                new GUIContent("Entity Prototype Definition"),
                prototypeDefinition,
                typeof(EntityPrototypeDefinition),
                allowSceneObjects: false);
            if (newPrototypeDefinition != prototypeDefinition)
            {
                entityPrefab = newPrototypeDefinition?.entityPrefab;
                prototypeDefinition = entityPrefab == null ? null : newPrototypeDefinition;
                entityPrefabProp.objectReferenceValue = entityPrefab;
            }

            so.ApplyModifiedProperties();
        }
    }
}
