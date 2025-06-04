using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

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
    }
}
