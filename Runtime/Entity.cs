using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Entity : UdonSharpBehaviour
    {
        [System.NonSerialized] public EntityPrototype prototype;
        [System.NonSerialized] public EntityData entityData;

        public EntityExtension[] extensions;

        public void InitFromEntityData(EntityData entityData)
        {
            this.entityData = entityData;
            prototype = entityData.entityPrototype;
            Transform t = this.transform;
            t.position = entityData.position;
            t.rotation = entityData.rotation;
            t.localRotation = entityData.rotation;
            // TODO: what to do about hidden?
            // TODO: handle parent entity
            // TODO: handle child entities
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
            {
                EntityExtension extension = extensions[i];
                extension.extensionData = allExtensionData[i];
                extension.InitFromExtensionData();
            }
        }
    }
}
