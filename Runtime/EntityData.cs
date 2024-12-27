using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityData : SerializableWannaBeClass
    {
        public override bool SupportsImportExport => false;
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

        // TODO: resolve after deserialization
        [System.NonSerialized] public uint unresolvedParentEntityId;
        [System.NonSerialized] public uint[] unresolvedChildEntitiesIds;

        /// <summary>
        /// <para>Sets everything except for <see cref="id"/>.</para>
        /// </summary>
        /// <param name="entity"></param>
        public void InitFromEntity(Entity entity)
        {
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
            foreach (SerializableWannaBeClass extensionData in allExtensionData)
                lockstep.WriteCustomNullableClass(extensionData);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
            entityPrototype = entitySystem.GetEntityPrototype(lockstep.ReadSmallUInt());
            id = lockstep.ReadSmallUInt();
            position = lockstep.ReadVector3();
            rotation = lockstep.ReadQuaternion();
            scale = lockstep.ReadVector3();
            createdByPlayerId = lockstep.ReadSmallUInt();
            lastUserPlayerId = lockstep.ReadSmallUInt();
            hidden = lockstep.ReadByte() != 0;
            unresolvedParentEntityId = lockstep.ReadSmallUInt();
            int childEntitiesLength = (int)lockstep.ReadSmallUInt();
            childEntities = new EntityData[childEntitiesLength];
            for (int i = 0; i < childEntitiesLength; i++)
                unresolvedChildEntitiesIds[i] = lockstep.ReadSmallUInt();
            string[] extensionClassNames = entityPrototype.ExtensionClassNames;
            int extensionsCount = extensionClassNames.Length;
            allExtensionData = new EntityExtensionData[extensionsCount];
            for (int i = 0; i < extensionsCount; i++)
                allExtensionData[i] = lockstep.ReadCustomNullableClass<EntityExtensionData>(extensionClassNames[i]);
        }
    }
}
