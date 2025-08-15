using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityPrototype : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] LockstepAPI lockstep;

        [SerializeField] private uint id;
        // TODO: replace this with a string that is a guid of an EntityPrototypeDefinition asset
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
        /// <summary>
        /// <para>Using this instead of a reference to an actual prefab asset avoids having to do prefab
        /// modifications in editor scripting and works around broken ui text field popups too.</para>
        /// <para>See this canny: https://feedback.vrchat.com/bug-reports/p/instantiated-input-fields-dont-open-the-vrc-keyboard</para>
        /// </summary>
        [SerializeField] private GameObject entityPrefabInst;
        /// <summary>
        /// <para>Once initialized this instance shall be immutable.</para>
        /// <para>It is used to reset any and all other instances of the same entity prototype to "factory
        /// defaults", which is required for entity pooling to work in a predictable fashion.</para>
        /// </summary>
        [SerializeField] private Entity defaultEntityInst;

        public GameObject EntityPrefabTemp => entityPrefab;

        public uint Id => id;
        public GameObject EntityPrefabInst => entityPrefabInst;
        public string PrototypeName => prototypeName;
        public string DisplayName => displayName;
        public string ShortDescription => shortDescription;
        public string LongDescription => longDescription;
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
