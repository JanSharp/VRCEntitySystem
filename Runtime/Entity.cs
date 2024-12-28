using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Entity : UdonSharpBehaviour
    {
        [System.NonSerialized] public LockstepAPI lockstep;
        [System.NonSerialized] public EntitySystem entitySystem;
        [System.NonSerialized] public WannaBeClassesManager wannaBeClasses;
        [System.NonSerialized] public EntityPrototype prototype;
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public int instanceIndex;

        public EntityExtension[] extensions;

        public void InitFromEntityData(EntityData entityData)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  InitFromEntityData");
            #endif
            lockstep = entityData.lockstep;
            entitySystem = entityData.entitySystem;
            prototype = entityData.entityPrototype;
            this.entityData = entityData;
            Transform t = this.transform;
            t.position = entityData.position;
            t.rotation = entityData.rotation;
            t.localScale = entityData.scale;
            // TODO: what to do about hidden?
            // TODO: handle parent entity
            // TODO: handle child entities
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
            {
                EntityExtension extension = extensions[i];
                extension.lockstep = lockstep;
                extension.entitySystem = entitySystem;
                extension.entity = this;
                EntityExtensionData extensionData = allExtensionData[i];
                if (extensionData != null)
                {
                    extension.extensionData = extensionData;
                    extensionData.extension = extension;
                    extension.InitFromExtensionData();
                }
                else
                {
                    extensionData = (EntityExtensionData)wannaBeClasses.NewDynamic(prototype.ExtensionClassNames[i]);
                    allExtensionData[i] = extensionData;
                    extension.extensionData = extensionData;
                    extensionData.extension = extension;
                    extensionData.InitFromExtension();
                }
            }
        }
    }
}
