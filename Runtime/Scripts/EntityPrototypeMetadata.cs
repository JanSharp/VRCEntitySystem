using UdonSharp;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityPrototypeMetadata : WannaBeClass
    {
        [System.NonSerialized] public uint id;
        [System.NonSerialized] public string prototypeName;
        [System.NonSerialized] public string displayName;
        [System.NonSerialized] public uint[] localExtensionIds;

        [System.NonSerialized] public EntityPrototype entityPrototype;
        [System.NonSerialized] public int[] resolvedExtensionIndexes;
        [System.NonSerialized] public string[] resolvedExtensionClassNames;

        public override bool WannaBeClassSupportsPooling => true;
        public override void ResetWannaBeClassToDefault()
        {
            id = default;
            prototypeName = default;
            displayName = default;
            localExtensionIds = default;

            entityPrototype = default;
            resolvedExtensionIndexes = default;
            resolvedExtensionClassNames = default;
        }
    }
}
