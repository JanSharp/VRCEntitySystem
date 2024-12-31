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

        public void Setup(int extensionIndex, LockstepAPI lockstep, EntitySystem entitySystem, Entity entity)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityExtension  Setup");
            #endif
            this.extensionIndex = extensionIndex;
            this.lockstep = lockstep;
            this.entitySystem = entitySystem;
            this.entity = entity;
        }

        public virtual void InitFromExtensionData() => ApplyExtensionData();
        public abstract void ApplyExtensionData();

        public ulong SendExtensionInputAction(string methodName) => entitySystem.SendExtensionInputAction(this, methodName);
    }
}
