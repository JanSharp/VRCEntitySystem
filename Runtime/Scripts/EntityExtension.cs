using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityExtension : UdonSharpBehaviour
    {
        [HideInInspector][SingletonReference] public LockstepAPI lockstep;
        [HideInInspector][SingletonReference] public EntitySystem entitySystem;
        [System.NonSerialized] public int extensionIndex;
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public Entity entity;
        [System.NonSerialized] public EntityExtensionData extensionData;

        public void InternalSetup(int extensionIndex, Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityExtension  InternalSetup");
#endif
            this.extensionIndex = extensionIndex;
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
