using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityPrototype : Prototype
    {
        [HideInInspector] [SerializeField] [SingletonReference] LockstepAPI lockstep;
        [SerializeField] private string prototypeName;
        [SerializeField] private string displayName;
        [SerializeField] private string shortDescription;
        [SerializeField] private string longDescription;
        [SerializeField] private GameObject entityPrefab;
        [SerializeField] private uint[] localExtensionIds;
        [SerializeField] private string[] extensionDataClassNames;

        public override string PrototypeName => prototypeName;
        public override string DisplayName => displayName;
        public override string ShortDescription => shortDescription;
        public override string LongDescription => longDescription;
        public GameObject EntityPrefab => entityPrefab;
        public uint[] LocalExtensionIds => localExtensionIds;
        public string[] ExtensionDataClassNames => extensionDataClassNames;

        public void ExportMetadata()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPrototype  ExportMetadata");
            #endif
            lockstep.WriteSmallUInt(Id);
            lockstep.WriteString(prototypeName);
            lockstep.WriteString(displayName);
            lockstep.WriteSmallUInt((uint)localExtensionIds.Length);
            foreach (uint id in localExtensionIds)
                lockstep.WriteSmallUInt(id);
        }
    }

    public static class EntityPrototypeStatics
    {
        public static EntityPrototypeMetadata ImportMetadata(WannaBeClassesManager wannaBeClasses, LockstepAPI lockstep, EntitySystem entitySystem)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPrototype  ImportMetadata");
            #endif
            EntityPrototypeMetadata metadata = wannaBeClasses.New<EntityPrototypeMetadata>(nameof(EntityPrototypeMetadata));
            metadata.id = lockstep.ReadSmallUInt();
            metadata.prototypeName = lockstep.ReadString();
            metadata.displayName = lockstep.ReadString();
            int length = (int)lockstep.ReadSmallUInt();
            metadata.localExtensionIds = new uint[length];
            metadata.resolvedExtensionIndexes = new int[length];
            metadata.resolvedExtensionClassNames = new string[length];
            for (int i = 0; i < length; i++)
                metadata.localExtensionIds[i] = lockstep.ReadSmallUInt();

            if (entitySystem.TryGetEntityPrototype(metadata.prototypeName, out EntityPrototype prototype))
            {
                metadata.entityPrototype = prototype;
                for (int i = 0; i < length; i++)
                {
                    int index = System.Array.IndexOf(prototype.LocalExtensionIds, metadata.localExtensionIds[i]);
                    if (index == -1)
                        continue;
                    metadata.resolvedExtensionIndexes[i] = index;
                    metadata.resolvedExtensionClassNames[i] = prototype.ExtensionDataClassNames[index];
                }
            }
            return metadata;
        }
    }
}
