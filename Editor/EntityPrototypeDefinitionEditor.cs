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
        private EntityPrototypeDefinition[] definitionsNotInScene;

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
            HashSet<string> guidsInScene = prototypesInScene
                .Select(p => p.PrototypeDefinitionGuid)
                .Where(g => !string.IsNullOrEmpty(g))
                .ToHashSet();
            definitionsNotInScene = targets
                .Cast<EntityPrototypeDefinition>()
                .Where(d => !guidsInScene.Contains(EntitySystemEditorUtil.GetAssetGuid(d)))
                .ToArray();
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

            // TODO: If a single one is selected show whether or not it is in the active scene.
            // TODO: when multiple are selected, list which ones are and are not in the active scene.

            if (definitionsNotInScene.Length != 0 && GUILayout.Button(new GUIContent("Add To Active Scene")))
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

            // if (GUILayout.Button(new GUIContent("Remove From Active Scene")))
            // {
            //     // TODO: impl
            // }
        }
    }
}
