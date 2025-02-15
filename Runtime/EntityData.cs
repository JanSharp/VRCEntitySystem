using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityData : WannaBeClass
    {
        [HideInInspector] [SingletonReference] public LockstepAPI lockstep;
        [HideInInspector] [SingletonReference] public EntitySystem entitySystem;
        [System.NonSerialized] public EntityPrototype entityPrototype;
        [System.NonSerialized] public Entity entity;
        [System.NonSerialized] public bool wasPreInstantiated = false;
        [System.NonSerialized] public uint id;
        private bool noPositionSync;
        private bool noRotationSync;
        private bool noScaleSync;
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

        public bool NoPositionSync
        {
            get => noPositionSync;
            set
            {
                noPositionSync = value;
                if (value)
                    position = Vector3.zero;
            }
        }
        public bool NoRotationSync
        {
            get => noRotationSync;
            set
            {
                noRotationSync = value;
                if (value)
                    rotation = Quaternion.identity;
            }
        }
        public bool NoScaleSync
        {
            get => noScaleSync;
            set
            {
                noScaleSync = value;
                if (value)
                    scale = Vector3.one;
            }
        }

        public EntityData WannaBeConstructor(uint id, EntityPrototype entityPrototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  WannaBeDestructor - id: {id}");
            #endif
            this.id = id;
            this.entityPrototype = entityPrototype;
            return this;
        }

        public override void WannaBeDestructor()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  WannaBeDestructor");
            #endif
            if (allExtensionData == null)
                return;
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.DecrementRefsCount();
        }

        public void SetEntity(Entity entity)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SetEntity");
            #endif
            this.entity = entity;
            entity.entityData = this;
        }

        public void InitFromEntity(Entity entity)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromEntity");
            #endif
            SetEntity(entity);
            Transform t = entity.transform;
            position = t.position;
            rotation = t.rotation;
            scale = t.localScale;
            createdByPlayerId = 0u;
            lastUserPlayerId = 0u;
            hidden = false;
            parentEntity = null;
        }

        public void InitAllExtensionDataFromExtensions()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitAllExtensionDataFromExtensions");
            #endif
            if (allExtensionData != null)
            {
                Debug.LogError($"[EntitySystem] Attempt to call InitAllExtensionDataFromExtensions on an "
                    + $"EntityData which already has existing extension data.");
                return;
            }
            string[] extensionClassNames = entityPrototype.ExtensionDataClassNames;
            int length = extensionClassNames.Length;
            allExtensionData = new EntityExtensionData[length];
            for (int i = 0; i < length; i++)
            {
                EntityExtension extension = entity.extensions[i];
                EntityExtensionData extensionData = EntityExtensionDataStatics
                    .New(WannaBeClasses, extensionClassNames[i], i, this, extension);
                extensionData.InitFromExtension();
            }
        }

        private void SerializeTransformValues()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SerializeTransformValues");
            #endif
            if (!noPositionSync)
                lockstep.WriteVector3(position);
            if (!noRotationSync)
                lockstep.WriteQuaternion(rotation);
            if (!noScaleSync)
                lockstep.WriteVector3(scale);
        }

        private void DeserializeTransformValue()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeTransformValue");
            #endif
            position = noPositionSync ? Vector3.zero : lockstep.ReadVector3();
            rotation = noRotationSync ? Quaternion.identity : lockstep.ReadQuaternion();
            scale = noScaleSync ? Vector3.one : lockstep.ReadVector3();
        }

        public void Serialize(bool isExport)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Serialize");
            #endif
            lockstep.WriteFlags(noPositionSync, noRotationSync, noScaleSync, hidden);
            SerializeTransformValues();
            lockstep.WriteSmallUInt(createdByPlayerId);
            lockstep.WriteSmallUInt(lastUserPlayerId);
            lockstep.WriteSmallUInt(parentEntity == null ? 0u : parentEntity.id);
            lockstep.WriteSmallUInt((uint)childEntities.Length);
            foreach (EntityData child in childEntities)
                lockstep.WriteSmallUInt(child.id);
            if (isExport)
                lockstep.WriteSmallUInt((uint)allExtensionData.Length);
            foreach (SerializableWannaBeClass extensionData in allExtensionData)
                lockstep.WriteCustomNullableClass(extensionData);
        }

        public void Deserialize(bool isImport, uint importedDataVersion)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Deserialize");
            #endif
            lockstep.ReadFlags(out noPositionSync, out noRotationSync, out noScaleSync, out hidden);
            DeserializeTransformValue();
            createdByPlayerId = lockstep.ReadSmallUInt();
            lastUserPlayerId = lockstep.ReadSmallUInt();
            unresolvedParentEntityId = lockstep.ReadSmallUInt();
            int childEntitiesLength = (int)lockstep.ReadSmallUInt();
            unresolvedChildEntitiesIds = new uint[childEntitiesLength];
            for (int i = 0; i < childEntitiesLength; i++)
                unresolvedChildEntitiesIds[i] = lockstep.ReadSmallUInt();
            if (isImport)
            {
                ResolveImportedParentEntityId();
                ResolveImportedChildEntityIds();
            }
            if (isImport)
                ImportAllExtensionData();
            else
                DeserializeAllExtensionData();
        }

        private void ResolveImportedParentEntityId()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  ResolveImportedParentEntityId");
            #endif
            entitySystem.TryGetRemappedImportedEntityData(unresolvedParentEntityId, out parentEntity);
            unresolvedParentEntityId = 0u;
        }

        private void ResolveImportedChildEntityIds()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  ResolveImportedChildEntityIds");
            #endif
            EntityData[] childEntities = new EntityData[unresolvedChildEntitiesIds.Length];
            int i = 0;
            foreach (uint childId in unresolvedChildEntitiesIds)
                if (entitySystem.TryGetRemappedImportedEntityData(childId, out EntityData data))
                    childEntities[i++] = data;
            if (i != childEntities.Length)
            {
                EntityData[] shortenedList = new EntityData[i];
                System.Array.Copy(childEntities, shortenedList, i);
                childEntities = shortenedList;
            }
            unresolvedChildEntitiesIds = null;
        }

        private void DeserializeAllExtensionData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeExtensions");
            #endif
            if (allExtensionData != null)
            {
                foreach (EntityExtensionData extensionData in allExtensionData)
                    lockstep.ReadCustomNullableClass(extensionData);
                return;
            }

            string[] extensionDataClassNames = entityPrototype.ExtensionDataClassNames;
            int extensionsCount = extensionDataClassNames.Length;
            allExtensionData = new EntityExtensionData[extensionsCount];
            for (int i = 0; i < extensionsCount; i++)
                lockstep.ReadCustomNullableClass(EntityExtensionDataStatics
                    .New(WannaBeClasses, extensionDataClassNames[i], i, this, entity == null ? null : entity.extensions[i]));
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
            if (allExtensionData == null)
                allExtensionData = new EntityExtensionData[entityPrototype.ExtensionDataClassNames.Length];
            for (int i = 0; i < length; i++)
            {
                string newExtensionClassName = importedMetadata.resolvedExtensionClassNames[i];
                if (newExtensionClassName == null)
                {
                    lockstep.SkipCustomClass(out uint dataVersion, out byte[] data);
                    continue;
                }
                int index = importedMetadata.resolvedExtensionIndexes[i];
                EntityExtensionData extensionData = allExtensionData[index];
                if (extensionData != null)
                {
                    if (!lockstep.ReadCustomNullableClass(extensionData))
                        extensionData.ImportedWithoutDeserialization();
                    continue;
                }
                extensionData = (EntityExtensionData)lockstep.ReadCustomNullableClass(newExtensionClassName);
                if (extensionData != null)
                {
                    extensionData.WannaBeConstructor(i, this, null);
                    allExtensionData[index] = extensionData;
                }
            }
        }
    }
}
