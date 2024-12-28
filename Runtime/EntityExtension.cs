using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityExtension : UdonSharpBehaviour
    {
        [System.NonSerialized] public int extensionIndex;
        [System.NonSerialized] public LockstepAPI lockstep;
        [System.NonSerialized] public EntitySystem entitySystem;
        [System.NonSerialized] public Entity entity;
        [System.NonSerialized] public EntityExtensionData extensionData;

        public abstract void InitFromExtensionData();

        public ulong SendExtensionInputAction(string methodName) => entitySystem.SendExtensionInputAction(this, methodName);
    }
}
