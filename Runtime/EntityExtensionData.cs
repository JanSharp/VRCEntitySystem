using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityExtensionData : SerializableWannaBeClass
    {
        [HideInInspector] [SingletonReference] public EntitySystem entitySystem;
        [System.NonSerialized] public EntityExtension extension;

        public abstract void InitFromExtension();
    }
}
