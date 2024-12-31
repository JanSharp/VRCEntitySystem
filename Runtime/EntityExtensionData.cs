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
        [System.NonSerialized] public int extensionIndex;
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public EntityExtension extension;

        public EntityExtensionData WannaBeConstructor(int extensionIndex, EntityData entityData, EntityExtension extension)
        {
            this.extensionIndex = extensionIndex;
            this.entityData = entityData;
            entityData.allExtensionData[extensionIndex] = this;
            this.extension = extension;
            if (extension != null)
                extension.extensionData = this;
            return this;
        }

        public void SetExtension(EntityExtension extension)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityExtensionData  SetExtension");
            #endif
            this.extension = extension;
            extension.extensionData = this;
        }

        public abstract void InitFromExtension();
    }

    public static class EntityExtensionDataStatics
    {
        public static EntityExtensionData New(
            WannaBeClassesManager wannaBeClasses,
            string extensionDataClassName,
            int extensionIndex,
            EntityData entityData,
            EntityExtension extension)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityExtensionDataStatics  New - extensionDataClassName: {extensionDataClassName}, extensionIndex: {extensionIndex}");
            #endif
            return ((EntityExtensionData)wannaBeClasses.NewDynamic(extensionDataClassName))
                .WannaBeConstructor(extensionIndex, entityData, extension);
        }
    }
}
