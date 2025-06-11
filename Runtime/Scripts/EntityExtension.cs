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
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public Entity entity;
        [System.NonSerialized] public EntityExtensionData extensionData;

        public void InternalSetup(int extensionIndex, LockstepAPI lockstep, EntitySystem entitySystem, Entity entity)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityExtension  InternalSetup");
#endif
            this.extensionIndex = extensionIndex;
            this.lockstep = lockstep;
            this.entitySystem = entitySystem;
            // entityData = null; // Default.
            this.entity = entity;
            // extensionData = null; // Default.
        }

        public virtual void OnInstantiateDefaultInstance() => OnInstantiate();
        public virtual void OnInstantiate() { }
        public virtual void AssociateWithExtensionData() => ApplyExtensionData();
        public abstract void ApplyExtensionData();
        public abstract void DisassociateFromExtensionDataAndReset(EntityExtension defaultExtension);
        public virtual void OnDestroyExtension() { }
    }
}
