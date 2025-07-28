using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityPrototype : Prototype
    {
        [HideInInspector][SerializeField][SingletonReference] LockstepAPI lockstep;

        [SerializeField] private GameObject entityPrefab; // Used to resolve the reference to EntityPrototypeDefinition.
        // All of this is just a mirror of the EntityPrototypeDefinition.
        [SerializeField] private string prototypeName;
        [SerializeField] private string displayName;
        [SerializeField] private string shortDescription;
        [SerializeField] private string longDescription;
        [SerializeField] private Vector3 defaultScale;
        // TODO: this probably needs a "highest id" field just like build time id assignment does
        [SerializeField] private uint[] localExtensionIds;
        [SerializeField] private string[] extensionDataClassNames;
        // And this is not from EntityPrototypeDefinition.
        [SerializeField] private Entity defaultEntityInst;

        public GameObject EntityPrefab => entityPrefab;
        public override string PrototypeName => prototypeName;
        public override string DisplayName => displayName;
        public override string ShortDescription => shortDescription;
        public override string LongDescription => longDescription;
        public Vector3 DefaultScale => defaultScale;
        public uint[] LocalExtensionIds => localExtensionIds;
        public string[] ExtensionDataClassNames => extensionDataClassNames;
        public Entity DefaultEntityInst => defaultEntityInst;

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
