using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityExtension : UdonSharpBehaviour
    {
        [System.NonSerialized] public LockstepAPI lockstep; // TODO: initialize!
        [System.NonSerialized] public EntitySystem entitySystem; // TODO: initialize!
        [System.NonSerialized] public Entity entity; // TODO: initialize!
        [System.NonSerialized] public EntityExtensionData extensionData; // TODO: initialize!

        public abstract void InitFromExtensionData();

        public ulong SendExtensionInputAction(string methodName) => entitySystem.SendExtensionInputAction(this, methodName);
    }
}
