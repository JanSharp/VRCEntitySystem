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
                if (prototypesInSceneByGuid.TryGetValue(EntitySystemEditorUtil.GetAssetGuid(definition), out var prototype))
                    definitionsInScene.Add((prototype, definition));
                else
                    definitionsNotInScene.Add(definition);
        }

        private static int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            while (transform != null)
            {
                depth++;
                transform = transform.parent;
            }
            return depth;
        }

        private Transform FindCommonParent()
        {
            Transform parent = prototypesInScene.FirstOrDefault()?.transform?.parent;
            int depth = GetHierarchyDepth(parent);
            foreach (var prototype in prototypesInScene.Skip(1))
            {
                Transform currentParent = prototype.transform.parent;
                if (currentParent == parent)
                    continue;
                int currentDepth = GetHierarchyDepth(currentParent);
                while (currentDepth > depth)
                {
                    currentParent = currentParent.parent;
                    currentDepth--;
                }
                while (depth > currentDepth)
                {
                    parent = parent.parent;
                    depth--;
                }
                while (currentParent != parent)
                {
                    parent = parent.parent;
                    currentParent = currentParent.parent;
                    depth--;
                }
                if (depth == 0)
                    break;
            }
            return parent;
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
                        Transform parent = FindCommonParent();
                        foreach (var definition in definitionsNotInScene)
                        {
                            GameObject prototypeGo = new GameObject(definition.name);
                            Undo.RegisterCreatedObjectUndo(prototypeGo, "Add Entity Prototype To Active Scene");
                            if (parent != null)
                                prototypeGo.transform.SetParent(parent, worldPositionStays: false);
                            EntityPrototype prototype = UdonSharpUndo.AddComponent<EntityPrototype>(prototypeGo);
                            prototype.PrototypeDefinitionGuid = EntitySystemEditorUtil.GetAssetGuid(definition);
                        }
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
