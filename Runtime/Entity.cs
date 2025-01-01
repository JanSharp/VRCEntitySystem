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
            if (this.entityData != null)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromEntityData an Entity multiple times.");
            #endif
            entityData.SetEntity(this);
            ApplyEntityDataWithoutExtension();
            lockstep = entityData.lockstep;
            entitySystem = entityData.entitySystem;
            prototype = entityData.entityPrototype;
            this.entityData = entityData;
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
            {
                EntityExtension extension = extensions[i];
                extension.Setup(i, lockstep, entitySystem, this);
                EntityExtensionData extensionData = allExtensionData[i];
                if (extensionData != null)
                {
                    extensionData.SetExtension(extension);
                    extension.InitFromExtensionData();
                }
                else
                {
                    extensionData = (EntityExtensionData)wannaBeClasses.NewDynamic(prototype.ExtensionDataClassNames[i]);
                    allExtensionData[i] = extensionData;
                    extensionData.SetExtension(extension);
                    extensionData.InitFromExtension();
                }
            }
        }

        private void ApplyEntityDataWithoutExtension()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityDataWithoutExtension");
            #endif
            Transform t = this.transform;
            t.position = entityData.position;
            t.rotation = entityData.rotation;
            t.localScale = entityData.scale;
            // TODO: what to do about hidden?
            // TODO: handle parent entity
            // TODO: handle child entities
        }

        public void ApplyEntityData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityData");
            #endif
            ApplyEntityDataWithoutExtension();
            foreach (EntityExtension extension in extensions)
                extension.ApplyExtensionData();
        }

        public void Move()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  Move");
            #endif
            // TODO: Interpolate.
            this.transform.SetPositionAndRotation(entityData.position, entityData.rotation);
        }

        public void ApplyScale()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  ApplyScale");
            #endif
            // TODO: Interpolate.
            this.transform.localScale = entityData.scale;
        }
    }
}
