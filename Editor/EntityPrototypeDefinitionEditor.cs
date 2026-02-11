using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(EntityPrototypeDefinition))]
    public class EntityPrototypeDefinitionEditor : Editor
    {
        private EntityPrototype[] prototypesInScene;
        private List<(EntityPrototype prototype, EntityPrototypeDefinition definition)> definitionsInScene = new();
        private List<EntityPrototypeDefinition> definitionsNotInScene = new();

        public void OnEnable()
        {
            FindPrototypes();
            EditorApplication.hierarchyChanged -= OnHierarchyChanged; // I don't believe this is required.
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        public void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            FindPrototypes();
            Repaint();
        }

        private void FindPrototypes()
        {
            prototypesInScene = FindObjectsByType<EntityPrototype>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Dictionary<string, EntityPrototype> prototypesInSceneByGuid = prototypesInScene
                .Where(p => !string.IsNullOrEmpty(p.PrototypeDefinitionGuid))
                .ToDictionary(p => p.PrototypeDefinitionGuid, p => p);
            definitionsInScene.Clear();
            definitionsNotInScene.Clear();
            foreach (var definition in targets.Cast<EntityPrototypeDefinition>())
                if (prototypesInSceneByGuid.TryGetValue(EditorUtil.GetAssetGuidOrEmpty(definition), out var prototype))
                    definitionsInScene.Add((prototype, definition));
                else
                    definitionsNotInScene.Add(definition);
        }

        public static Transform FindEntityPrototypesParent(IEnumerable<EntityPrototype> prototypes)
        {
            if (!prototypes.Any())
            {
                GameObject parentGo = new GameObject("EntityPrototypes");
                Undo.RegisterCreatedObjectUndo(parentGo, "Create EntityPrototypes Container");
                return parentGo.transform;
            }
            return EditorUtil.FindCommonParent(prototypes.Select(p => p.transform));
        }

        public static void AddEntityPrototypeToScene(EntityPrototypeDefinition definition, Transform parent, string undoActionName)
        {
            GameObject prototypeGo = new GameObject(definition.name);
            Undo.RegisterCreatedObjectUndo(prototypeGo, undoActionName);
            if (parent != null)
                prototypeGo.transform.SetParent(parent, worldPositionStays: false);
            EntityPrototype prototype = UdonSharpUndo.AddComponent<EntityPrototype>(prototypeGo);
            prototype.PrototypeDefinitionGuid = EditorUtil.GetAssetGuidOrEmpty(definition);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Selected definitions not in the "
                    + $"active scene: {definitionsNotInScene.Count}", EditorStyles.wordWrappedLabel);
                foreach (var definition in definitionsNotInScene)
                    EditorGUILayout.ObjectField(definition.name, definition, typeof(EntityPrototypeDefinition), allowSceneObjects: false);
                using (new EditorGUI.DisabledGroupScope(disabled: definitionsNotInScene.Count == 0))
                    if (GUILayout.Button(new GUIContent("Add To Active Scene")))
                    {
                        Transform parent = FindEntityPrototypesParent(prototypesInScene);
                        foreach (var definition in definitionsNotInScene)
                            AddEntityPrototypeToScene(definition, parent, "Add Entity Prototype To Active Scene");
                    }
            }

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Selected definitions in the "
                    + $"active scene: {definitionsInScene.Count}", EditorStyles.wordWrappedLabel);
                foreach (var def in definitionsInScene)
                    EditorGUILayout.ObjectField(def.definition.name, def.definition, typeof(EntityPrototypeDefinition), allowSceneObjects: false);
                using (new EditorGUI.DisabledGroupScope(disabled: definitionsInScene.Count == 0))
                    if (GUILayout.Button(new GUIContent("Remove From Active Scene")))
                        foreach (var def in definitionsInScene)
                            Undo.DestroyObjectImmediate(def.prototype.gameObject);
            }
        }
    }
}
