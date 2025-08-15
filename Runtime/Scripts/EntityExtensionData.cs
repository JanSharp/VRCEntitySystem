using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityExtensionData : SerializableWannaBeClass
    {
        [HideInInspector][SingletonReference] public EntitySystem entitySystem;
        [System.NonSerialized] public int extensionIndex;
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public Entity entity;
        [System.NonSerialized] public EntityExtension extension;

        public EntityExtensionData WannaBeConstructor(int extensionIndex, EntityData entityData)
        {
            this.extensionIndex = extensionIndex;
            this.entityData = entityData;
            entityData.allExtensionData[extensionIndex] = this;
            // entity = null; // Default.
            // extension = null; // Default.
            return this;
        }

        public void SetEntityAndExtension(Entity entity, EntityExtension extension)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityExtensionData  SetEntityAndExtension");
#endif
            this.entity = entity;
            this.extension = extension;
            extension.entityData = entityData;
            extension.extensionData = this;
        }

        /// <summary>
        /// <para>The given extension instance is immutable.</para>
        /// </summary>
        /// <param name="entityExtension"></param>
        public abstract void InitFromDefault(EntityExtension entityExtension);
        public abstract void InitFromPreInstantiated(EntityExtension entityExtension);
        public virtual void OnEntityExtensionDataCreated() { }
        public abstract void OnAssociatedWithExtension();
        public virtual void OnDisassociateFromExtension() { }
        public virtual void OnEntityExtensionDataDestroyed() { }
        public virtual void ImportedWithoutDeserialization() { } // TODO: maybe add On prefix, also add to lifecycle

        public ulong SendExtensionDataInputAction(string methodName) => entitySystem.SendExtensionDataInputAction(this, methodName);
    }

    public static class EntityExtensionDataStatics
    {
        public static EntityExtensionData New(
            WannaBeClassesManager wannaBeClasses,
            string extensionDataClassName,
            int extensionIndex,
            EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityExtensionDataStatics  New - extensionDataClassName: {extensionDataClassName}, extensionIndex: {extensionIndex}");
#endif
            return ((EntityExtensionData)wannaBeClasses.NewDynamic(extensionDataClassName))
                .WannaBeConstructor(extensionIndex, entityData);
        }
    }
}
