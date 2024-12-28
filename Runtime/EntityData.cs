using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityData : SerializableWannaBeClass
    {
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        [HideInInspector] [SingletonReference] public EntitySystem entitySystem;
        [System.NonSerialized] public EntityPrototype entityPrototype;
        [System.NonSerialized] public Entity entity;
        [System.NonSerialized] public uint id;
        [System.NonSerialized] public Vector3 position;
        [System.NonSerialized] public Quaternion rotation;
        [System.NonSerialized] public Vector3 scale;
        [System.NonSerialized] public uint createdByPlayerId;
        [System.NonSerialized] public uint lastUserPlayerId;
        [System.NonSerialized] public bool hidden;
        [System.NonSerialized] public EntityData parentEntity;
        [System.NonSerialized] public EntityData[] childEntities = new EntityData[0];
        [System.NonSerialized] public EntityExtensionData[] allExtensionData;

        [System.NonSerialized] public uint unresolvedParentEntityId;
        [System.NonSerialized] public uint[] unresolvedChildEntitiesIds;

        [System.NonSerialized] public EntityPrototypeMetadata importedMetadata;

        /// <summary>
        /// <para>Sets everything except for <see cref="id"/>.</para>
        /// </summary>
        /// <param name="entity"></param>
        public void InitFromEntity(Entity entity)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromEntity");
            #endif
            entityPrototype = entity.prototype;
            this.entity = entity;
            Transform t = entity.transform;
            position = t.position;
            rotation = t.rotation;
            scale = t.localScale;
            createdByPlayerId = 0u;
            lastUserPlayerId = 0u;
            hidden = false;
            parentEntity = null;
            allExtensionData = new EntityExtensionData[entityPrototype.ExtensionClassNames.Length];
        }

        public override void Serialize(bool isExport)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Serialize");
            #endif
            lockstep.WriteSmallUInt(entityPrototype.Id);
            lockstep.WriteSmallUInt(id);
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            lockstep.WriteVector3(scale);
            lockstep.WriteSmallUInt(createdByPlayerId);
            lockstep.WriteSmallUInt(lastUserPlayerId);
            lockstep.WriteByte((byte)(hidden ? 1 : 0));
            lockstep.WriteSmallUInt(parentEntity == null ? 0u : parentEntity.id);
            lockstep.WriteSmallUInt((uint)childEntities.Length);
            foreach (EntityData child in childEntities)
                lockstep.WriteSmallUInt(child.id);
            if (isExport)
                lockstep.WriteSmallUInt((uint)allExtensionData.Length);
            foreach (SerializableWannaBeClass extensionData in allExtensionData)
                lockstep.WriteCustomNullableClass(extensionData);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Deserialize");
            #endif
            uint prototypeId = lockstep.ReadSmallUInt();
            if (isImport)
            {
                importedMetadata = entitySystem.GetImportedPrototypeMetadata(prototypeId);
                entityPrototype = importedMetadata.entityPrototype; // Could be null.
            }
            else
                entityPrototype = entitySystem.GetEntityPrototype(prototypeId);
            id = lockstep.ReadSmallUInt();
            position = lockstep.ReadVector3();
            rotation = lockstep.ReadQuaternion();
            scale = lockstep.ReadVector3();
            createdByPlayerId = lockstep.ReadSmallUInt();
            lastUserPlayerId = lockstep.ReadSmallUInt();
            hidden = lockstep.ReadByte() != 0;
            unresolvedParentEntityId = lockstep.ReadSmallUInt();
            int childEntitiesLength = (int)lockstep.ReadSmallUInt();
            unresolvedChildEntitiesIds = new uint[childEntitiesLength];
            for (int i = 0; i < childEntitiesLength; i++)
                unresolvedChildEntitiesIds[i] = lockstep.ReadSmallUInt();
            if (isImport)
                ImportAllExtensionData();
            else
                DeserializeAllExtensionData();
        }

        private void DeserializeAllExtensionData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeExtensions");
            #endif
            string[] extensionClassNames = entityPrototype.ExtensionClassNames;
            int extensionsCount = extensionClassNames.Length;
            allExtensionData = new EntityExtensionData[extensionsCount];
            for (int i = 0; i < extensionsCount; i++)
                allExtensionData[i] = lockstep.ReadCustomNullableClass<EntityExtensionData>(extensionClassNames[i]);
        }

        private void ImportAllExtensionData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  ImportAllExtensionData");
            #endif
            int length = (int)lockstep.ReadSmallUInt();
            if (entityPrototype == null)
            {
                for (int i = 0; i < length; i++)
                    lockstep.SkipCustomClass(out uint dataVersion, out byte[] data);
                return;
            }
            allExtensionData = new EntityExtensionData[entityPrototype.ExtensionClassNames.Length];
            for (int i = 0; i < length; i++)
            {
                string newExtensionClassName = importedMetadata.resolvedExtensionClassNames[i];
                if (newExtensionClassName == null)
                    lockstep.SkipCustomClass(out uint dataVersion, out byte[] data);
                else
                    allExtensionData[importedMetadata.resolvedExtensionIndexes[i]] = lockstep.ReadCustomNullableClass<EntityExtensionData>(newExtensionClassName);
            }
        }
    }
}
