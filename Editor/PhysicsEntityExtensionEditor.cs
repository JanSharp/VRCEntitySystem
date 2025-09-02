using UdonSharpEditor;
using UnityEditor;

namespace JanSharp
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PhysicsEntityExtension))]
    public class PhysicsEntityExtensionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets, skipLine: true))
                return;
        }
    }
}
