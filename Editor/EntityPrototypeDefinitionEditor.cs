using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(EntityPrototypeDefinition))]
    public class EntityPrototypeDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            // TODO: If a single one is selected show whether or not it is in the active scene.
            // TODO: when multiple are selected, list which ones are and are not in the active scene.
            if (GUILayout.Button(new GUIContent("Add To Active Scene")))
            {
                // TODO: impl
            }
            if (GUILayout.Button(new GUIContent("Remove From Active Scene")))
            {
                // TODO: impl
            }
        }
    }
}
