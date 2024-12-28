using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript]
    public class EntitySystem : LockstepGameState
    {
        public override string GameStateInternalName => "jansharp.entity-system";
        public override string GameStateDisplayName => "Entity System";
        public override bool GameStateSupportsImportExport => true;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;

        [SingletonReference] [HideInInspector] [SerializeField] private LockstepAPI lockstep;
        [SingletonReference] [HideInInspector] [SerializeField] private WannaBeClassesManager wannaBeClasses;
        public EntityPrototype[] entityPrototypes;
        private DataDictionary entityPrototypesById = new DataDictionary();
        private DataDictionary entityPrototypesByName = new DataDictionary();
        public Entity[] preInstantiatedEntityInstances;
        public uint[] preInstantiatedEntityInstanceIds;
        public EntityPrototype[] preInstantiatedEntityInstancePrototypes;
        private Entity[] entityInstances = new Entity[ArrList.MinCapacity];
        private DataDictionary entityInstancesById = new DataDictionary();
        private int entityInstancesCount = 0;
        private uint nextEntityId = 1u;

        private void Start()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Start");
            #endif
            InitEntityPrototypes();
            InitPreInstantiatedEntities();
        }

        private void InitEntityPrototypes()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitEntityPrototypes");
            #endif
            foreach (EntityPrototype prototype in entityPrototypes)
            {
                entityPrototypesById.Add(prototype.Id, prototype);
                entityPrototypesByName.Add(prototype.PrototypeName, prototype);
            }
        }

        private void InitPreInstantiatedEntities()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitPreInstantiatedEntities");
            #endif
            int length = preInstantiatedEntityInstances.Length;
            ArrList.EnsureCapacity(ref entityInstances, length);
            for (int i = 0; i < length; i++)
            {
                Entity entity = preInstantiatedEntityInstances[i];
                if (entity == null)
                    continue;
                EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[i];
                entity.prototype = prototype;
                EntityData entityData = NewEntityData();
                entity.entityData = entityData;
                entityData.InitFromEntity(entity);
                entityData.id = preInstantiatedEntityInstanceIds[i];
                // TODO: handle extensions in many ways
                ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
                entityInstancesById.Add(entityData.id, entity);
            }
        }

        private void InitNextEntityId()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitNextEntityId");
            #endif
            nextEntityId = preInstantiatedEntityInstanceIds.Length == 0
                ? 1u
                : (preInstantiatedEntityInstanceIds[preInstantiatedEntityInstanceIds.Length - 1] + 1u);
        }

        private EntityData NewEntityData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  NewEntityData");
            #endif
            return wannaBeClasses.New<EntityData>(nameof(EntityData));
        }

        public EntityPrototype GetEntityPrototype(uint prototypeId) => (EntityPrototype)entityPrototypesById[prototypeId].Reference;
        public bool TryGetEntityPrototype(uint prototypeId, out EntityPrototype entityPrototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetEntityPrototype");
            #endif
            if (entityPrototypesById.TryGetValue(prototypeId, out DataToken token))
            {
                entityPrototype = (EntityPrototype)token.Reference;
                return true;
            }
            entityPrototype = null;
            return false;
        }

        public EntityPrototype GetEntityPrototype(string prototypeName) => (EntityPrototype)entityPrototypesByName[prototypeName].Reference;
        public bool TryGetEntityPrototype(string prototypeName, out EntityPrototype entityPrototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetEntityPrototype");
            #endif
            if (entityPrototypesByName.TryGetValue(prototypeName, out DataToken token))
            {
                entityPrototype = (EntityPrototype)token.Reference;
                return true;
            }
            entityPrototype = null;
            return false;
        }

        public void SendCreateEntityIA(uint prototypeId)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendCreateEntityIA");
            #endif
            lockstep.WriteSmallUInt(prototypeId);
            lockstep.SendInputAction(createEntityIAId);
        }

        [HideInInspector] [SerializeField] private uint createEntityIAId;
        [LockstepInputAction(nameof(createEntityIAId))]
        public void OnCreateEntityIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnCreateEntityIA");
            #endif
            uint prototypeId = lockstep.ReadSmallUInt();
            CreateEntity(prototypeId);
        }

        public Entity CreateEntity(uint prototypeId)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            if (!TryGetEntityPrototype(prototypeId, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype);
        }

        public Entity CreateEntity(string prototypeName)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            if (!TryGetEntityPrototype(prototypeName, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype);
        }

        public Entity CreateEntity(EntityPrototype prototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            Entity entity = InstantiateEntity(prototype);
            EntityData entityData = NewEntityData();
            entity.entityData = entityData;
            entityData.InitFromEntity(entity);
            entityData.id = nextEntityId++;
            string[] extensionClassNames = prototype.ExtensionClassNames;
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            for (int i = 0; i < extensionClassNames.Length; i++)
            {
                EntityExtension extension = entity.extensions[i];
                extension.lockstep = lockstep;
                extension.entitySystem = this;
                extension.entity = entity;
                EntityExtensionData extensionData = wannaBeClasses.New<EntityExtensionData>(extensionClassNames[i]);
                allExtensionData[i] = extensionData;
                extension.extensionData = extensionData;
                extensionData.extension = extension;
                extensionData.InitFromExtension();
            }
            ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
            entityInstancesById.Add(entityData.id, entity);
            return entity;
        }

        public void WriteEntityExtensionReference(EntityExtension extension)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityExtensionReference");
            #endif
            lockstep.WriteSmallUInt(extension.entity.entityData.id);
            lockstep.WriteSmallUInt((uint)System.Array.IndexOf(extension.entity.extensions, extension));
        }

        public EntityExtension ReadEntityExtensionReferenceDynamic()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityExtensionReferenceDynamic");
            #endif
            uint entityId = lockstep.ReadSmallUInt();
            int index = (int)lockstep.ReadSmallUInt();
            if (!entityInstancesById.TryGetValue(entityId, out DataToken entityToken))
                return null;
            return ((Entity)entityToken.Reference).extensions[index];
        }

        public ulong SendExtensionInputAction(EntityExtension extension, string methodName)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendExtensionInputAction");
            #endif
            byte[] buffer = new byte[10 + (5 + methodName.Length)]; // No multi byte characters, so this is fine.
            int bufferSize = 0;
            DataStream.WriteSmall(ref buffer, ref bufferSize, (uint)extension.entity.entityData.id);
            DataStream.WriteSmall(ref buffer, ref bufferSize, (uint)System.Array.IndexOf(extension.entity.extensions, extension));
            DataStream.Write(ref buffer, ref bufferSize, methodName); // TODO: use build time generated id instead.
            int iaSize = lockstep.WriteStreamPosition;
            lockstep.ShiftWriteStream(0, bufferSize, iaSize);
            lockstep.WriteStreamPosition = 0;
            lockstep.WriteBytes(buffer, 0, bufferSize);
            lockstep.WriteStreamPosition = bufferSize + iaSize;
            return lockstep.SendInputAction(onExtensionInputActionIAId);
        }

        [HideInInspector] [SerializeField] private uint onExtensionInputActionIAId;
        [LockstepInputAction(nameof(onExtensionInputActionIAId))]
        public void OnExtensionInputActionIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnExtensionInputActionIA");
            #endif
            EntityExtension extension = ReadEntityExtensionReferenceDynamic();
            if (extension == null)
                return;
            string methodName = lockstep.ReadString();
            extension.SendCustomEvent(methodName);
        }

        private Entity InstantiateEntity(EntityPrototype prototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InstantiateEntity");
            #endif
            GameObject entityGo = Instantiate(prototype.EntityPrefab);
            Entity entity = entityGo.GetComponent<Entity>();
            entity.lockstep = lockstep;
            entity.entitySystem = this;
            entity.wannaBeClasses = wannaBeClasses;
            entity.prototype = prototype;
            return entity;
        }

        private void Export()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Export");
            #endif
            lockstep.WriteSmallUInt((uint)entityPrototypes.Length);
            foreach (EntityPrototype prototype in entityPrototypes)
                prototype.ExportMetadata();

            lockstep.WriteSmallUInt((uint)entityInstancesCount);
            for (int i = 0; i < entityInstancesCount; i++)
            {
                Entity entity = entityInstances[i];
                lockstep.WriteCustomClass(entity.entityData);
            }
        }

        private DataDictionary importedPrototypeMetadataById;
        public EntityPrototypeMetadata GetImportedPrototypeMetadata(uint prototypeId) => (EntityPrototypeMetadata)importedPrototypeMetadataById[prototypeId].Reference;
        public bool TryGetImportedPrototypeMetadata(uint prototypeId, out EntityPrototypeMetadata metadata)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetImportedMetadata");
            #endif
            if (importedPrototypeMetadataById.TryGetValue(prototypeId, out DataToken token))
            {
                metadata = (EntityPrototypeMetadata)token.Reference;
                return true;
            }
            metadata = null;
            return false;
        }

        private DataDictionary remappedImportedEntityData;
        public EntityData GetRemappedImportedEntityData(uint importedId) => (EntityData)remappedImportedEntityData[importedId].Reference;
        public bool TryGetRemappedImportedEntityData(uint importedId, out EntityData remappedEntityData)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetRemappedImportedEntityData");
            #endif
            if (remappedImportedEntityData.TryGetValue(importedId, out DataToken token))
            {
                remappedEntityData = (EntityData)token.Reference;
                return true;
            }
            remappedEntityData = null;
            return false;
        }

        private void ReadAllImportedPrototypeMetadata()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadAllImportedPrototypeMetadata");
            #endif
            int length = (int)lockstep.ReadSmallUInt();
            EntityPrototypeMetadata[] allImportedMetadata = new EntityPrototypeMetadata[length];
            for (int i = 0; i < length; i++)
            {
                EntityPrototypeMetadata metadata = EntityPrototypeStatics.ImportMetadata(wannaBeClasses, lockstep, this);
                allImportedMetadata[i] = metadata;
                importedPrototypeMetadataById.Add(metadata.id, metadata);
            }
        }

        private EntityData[] ReadAllImportedEntityData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadAllImportedEntityData");
            #endif
            int length = (int)lockstep.ReadSmallUInt();
            EntityData[] allImportedEntityData = new EntityData[length];
            for (int i = 0; i < length; i++)
            {
                EntityData entityData = lockstep.ReadCustomClass<EntityData>(nameof(EntityData));
                allImportedEntityData[i] = entityData;
                if (entityData.entityPrototype == null)
                    continue;
                uint importedId = entityData.id;
                entityData.id = nextEntityId++;
                remappedImportedEntityData.Add(importedId, entityData);
            }
            return allImportedEntityData;
        }

        private void ResolveImportedEntityIdReferences(EntityData[] allImportedEntityData)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ResolveImportedEntityIdReferences");
            #endif
            foreach (EntityData entityData in allImportedEntityData)
            {
                if (entityData.entityPrototype == null)
                    continue;

                if (TryGetRemappedImportedEntityData(entityData.unresolvedParentEntityId, out EntityData data))
                    entityData.parentEntity = data;
                entityData.unresolvedParentEntityId = 0u;

                EntityData[] childEntities = new EntityData[entityData.unresolvedChildEntitiesIds.Length];
                int i = 0;
                foreach (uint childId in entityData.unresolvedChildEntitiesIds)
                    if (TryGetRemappedImportedEntityData(childId, out data))
                        childEntities[i++] = data;
                if (i != childEntities.Length)
                {
                    EntityData[] shortenedList = new EntityData[i];
                    System.Array.Copy(childEntities, shortenedList, i);
                    childEntities = shortenedList;
                }
                entityData.childEntities = childEntities;
                entityData.unresolvedChildEntitiesIds = null;
            }
        }

        private void CreateImportedEntities(EntityData[] allImportedEntityData)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateImportedEntities");
            #endif
            foreach (EntityData entityData in allImportedEntityData)
            {
                if (entityData.entityPrototype == null)
                    continue;
                Entity entity = InstantiateEntity(entityData.entityPrototype);
                entity.InitFromEntityData(entityData);
                ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
                entityInstancesById.Add(entityData.id, entity);
            }
        }

        private void DeleteEntityPrototypeMetadataClasses(EntityData[] allImportedEntityData)
        {
            DataList list = importedPrototypeMetadataById.GetValues();
            for (int i = 0; i < list.Count; i++)
                ((EntityPrototypeMetadata)list[i].Reference).Delete();
            foreach (EntityData entityData in allImportedEntityData)
                entityData.importedMetadata = null; // Clear reference to empty unity object reference object so that can get GCed too.
        }

        private string Import(uint importedDataVersion)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Import");
            #endif
            importedPrototypeMetadataById = new DataDictionary();
            remappedImportedEntityData = new DataDictionary();

            // TODO: Delete existing entities.

            ReadAllImportedPrototypeMetadata();
            EntityData[] allImportedEntityData = ReadAllImportedEntityData();
            ResolveImportedEntityIdReferences(allImportedEntityData);
            CreateImportedEntities(allImportedEntityData);
            DeleteEntityPrototypeMetadataClasses(allImportedEntityData);

            importedPrototypeMetadataById = null;
            remappedImportedEntityData = null;
            return null;
        }

        public override void SerializeGameState(bool isExport)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeGameState");
            #endif
            if (isExport)
            {
                Export();
                return;
            }

            lockstep.WriteSmallUInt(nextEntityId);

            lockstep.WriteSmallUInt((uint)entityInstancesCount);
            for (int i = 0; i < entityInstancesCount; i++)
            {
                Entity entity = entityInstances[i];
                lockstep.WriteCustomClass(entity.entityData);
            }
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeGameState");
            #endif
            if (isImport)
                return Import(importedDataVersion);

            nextEntityId = lockstep.ReadSmallUInt();

            int count = (int)lockstep.ReadSmallUInt();
            ArrList.EnsureCapacity(ref entityInstances, count);
            for (int i = 0; i < count; i++)
            {
                EntityData entityData = lockstep.ReadCustomClass<EntityData>(nameof(EntityData));
                // TODO: check for pre instantiated entity id.
                Entity entity = InstantiateEntity(entityData.entityPrototype);
                entity.InitFromEntityData(entityData);
                ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
                entityInstancesById.Add(entityData.id, entity);
            }

            return null;
        }
    }

    public static class EntitySystemExtension
    {
        public static T ReadEntityExtensionReference<T>(this EntitySystem entitySystem)
            where T : EntityExtension
        {
            return (T)entitySystem.ReadEntityExtensionReferenceDynamic();
        }
    }
}
