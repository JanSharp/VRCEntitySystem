using UdonSharpEditor;
using UnityEditor;

namespace JanSharp
{
    public static class EntityOnBuild
    {
        [OrderedInitializeOnLoad]
        private static void OnAssemblyLoad()
        {
            OnBuildUtil.RegisterType<Entity>(OnBuild, order: -5);
        }

        private static bool OnBuild(Entity entity)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(entity.gameObject))
                return true; // Invalid, but this gets validated later by EntitySystem editor scripting.
            SerializedObject so = new SerializedObject(entity);
            // The list of extensions can gain overrides when somebody modifies a prefab instance in the scene
            // and then applies those modifications to the prefab. The removal of an extension leaves a
            // missing extension reference in the list, which Unity appears to not apply even when hitting the
            // apply all overrides button.
            // Which, without this revert here, would leave a null extension in the list, which is invalid.
            PrefabUtility.RevertPropertyOverride(so.FindProperty(nameof(Entity.extensions)), InteractionMode.AutomatedAction);
            // Seems like there is no need to call so.ApplyModifiedProperties() here.
            return true;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(Entity))]
    public class EntityEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets, skipLine: true))
                return;
        }
    }
}
