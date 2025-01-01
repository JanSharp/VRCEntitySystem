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
        /// <summary>
        /// <para>Must be set by editor scripting, otherwise ids for pre instantiated entities could end up being reused.</para>
        /// </summary>
        public uint highestPreInstantiatedEntityId = 1u;
        private uint highestImportedPreInstantiatedEntityId;
        private uint nextEntityId;
        public string[] rawExtensionMethodNamesLut;
        /// <summary>
        /// <para><see cref="string"/> => <see cref="string[]"/></para>
        /// </summary>
        private DataDictionary extensionMethodNamesLut = new DataDictionary();

        private void Start()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Start");
            #endif
            InitEntityPrototypes();
            InitExtensionIANameLut();
            SetupPreInstantiatedEntities();
            nextEntityId = highestPreInstantiatedEntityId + 1u;
        }

        [LockstepEvent(LockstepEventType.OnInit)]
        public void OnInit()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnInit");
            #endif
            // Init pre instantiated entities.
            InitPreInstantiatedEntities();
        }

        [LockstepEvent(LockstepEventType.OnClientBeginCatchUp)]
        public void OnClientBeginCatchUp()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnClientBeginCatchUp");
            #endif
            // Load existing game state into pre instantiated entities.
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

        private void SetupPreInstantiatedEntities()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SetupPreInstantiatedEntities");
            #endif
            int length = preInstantiatedEntityInstances.Length;
            for (int i = 0; i < length; i++)
            {
                Entity entity = preInstantiatedEntityInstances[i];
                if (entity == null)
                    continue;
                SetupNewEntity(entity, preInstantiatedEntityInstancePrototypes[i]);
                RegisterEntity(entity, preInstantiatedEntityInstanceIds[i]);
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
                EntityData entityData = NewEntityData(preInstantiatedEntityInstanceIds[i], entity.prototype);
                entityData.wasPreInstantiated = true;
                entityData.InitFromEntity(entity);
                entityData.InitAllExtensionDataFromExtensions();
            }
        }

        private void InitExtensionIANameLut()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitExtensionIANameLut");
            #endif
            int i = 0;
            int length = rawExtensionMethodNamesLut.Length;
            while (i < length)
            {
                string className = rawExtensionMethodNamesLut[i++];
                int methodNamesCount = int.Parse(rawExtensionMethodNamesLut[i++]);
                string[] methodNames = new string[methodNamesCount];
                System.Array.Copy(rawExtensionMethodNamesLut, i, methodNames, 0, methodNamesCount);
                i += methodNamesCount;
                extensionMethodNamesLut.Add(className, new DataToken(methodNames));
            }
        }

        public bool IsPreInstantiatedEntityId(uint id, bool isImport)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  IsPreInstantiatedEntityId - id: {id}");
            #endif
            return id <= highestPreInstantiatedEntityId
                && (!isImport || id <= highestImportedPreInstantiatedEntityId);
        }

        private void RegisterEntity(Entity entity, uint entityId)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  RegisterEntity");
            #endif
            entity.instanceIndex = entityInstancesCount;
            ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
            entityInstancesById.Add(entityId, entity);
        }

        private EntityData NewEntityData(uint id, EntityPrototype entityPrototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  NewEntityData");
            #endif
            return wannaBeClasses.New<EntityData>(nameof(EntityData))
                .WannaBeConstructor(id, entityPrototype);
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

        public Entity GetEntityInstance(uint entityId) => (Entity)entityInstancesById[entityId].Reference;
        public bool TryGetEntityInstance(uint entityId, out Entity entity)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetEntityInstance");
            #endif
            if (entityInstancesById.TryGetValue(entityId, out DataToken token))
            {
                entity = (Entity)token.Reference;
                return true;
            }
            entity = null;
            return false;
        }

        public void SendCreateEntityIA(uint prototypeId, Vector3 position, Quaternion rotation)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendCreateEntityIA");
            #endif
            lockstep.WriteSmallUInt(prototypeId);
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
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
            Vector3 position = lockstep.ReadVector3();
            Quaternion rotation = lockstep.ReadQuaternion();
            CreateEntity(prototypeId, position, rotation);
        }

        public Entity CreateEntity(uint prototypeId, Vector3 position, Quaternion rotation)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            if (!TryGetEntityPrototype(prototypeId, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype, position, rotation);
        }

        public Entity CreateEntity(string prototypeName, Vector3 position, Quaternion rotation)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            if (!TryGetEntityPrototype(prototypeName, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype, position, rotation);
        }

        public Entity CreateEntity(EntityPrototype prototype, Vector3 position, Quaternion rotation)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            uint id = nextEntityId++;
            Entity entity = InstantiateEntity(prototype, id);
            entity.transform.SetPositionAndRotation(position, rotation);
            EntityData entityData = NewEntityData(id, prototype);
            entityData.InitFromEntity(entity);
            entityData.InitAllExtensionDataFromExtensions();
            return entity;
        }

        private Entity InstantiateEntity(EntityPrototype prototype, uint entityId)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InstantiateEntity");
            #endif
            GameObject entityGo = Instantiate(prototype.EntityPrefab);
            Entity entity = entityGo.GetComponent<Entity>();
            SetupNewEntity(entity, prototype);
            RegisterEntity(entity, entityId);
            return entity;
        }

        private void SetupNewEntity(Entity entity, EntityPrototype prototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            entity.lockstep = lockstep;
            entity.entitySystem = this;
            entity.wannaBeClasses = wannaBeClasses;
            entity.prototype = prototype;
            int length = entity.extensions.Length;
            for (int i = 0; i < length; i++)
                entity.extensions[i].Setup(i, lockstep, this, entity);
        }

        public void SendMoveEntityIA(uint entityId, Vector3 position, Quaternion rotation)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendMoveEntityIA");
            #endif
            lockstep.WriteSmallUInt(entityId);
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            lockstep.SendInputAction(moveEntityIAId);
        }

        [HideInInspector] [SerializeField] private uint moveEntityIAId;
        [LockstepInputAction(nameof(moveEntityIAId))]
        public void OnMoveEntityIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnMoveEntityIA");
            #endif
            uint entityId = lockstep.ReadSmallUInt();
            Vector3 position = lockstep.ReadVector3();
            Quaternion rotation = lockstep.ReadQuaternion();
            if (!TryGetEntityInstance(entityId, out Entity entity))
                return;
            EntityData entityData = entity.entityData;
            entityData.position = position;
            entityData.rotation = rotation;
            entity.Move();
        }

        public void SendSetEntityScaleIA(uint entityId, Vector3 scale)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendSetEntityScaleIA");
            #endif
            lockstep.WriteSmallUInt(entityId);
            lockstep.WriteVector3(scale);
            lockstep.SendInputAction(setEntityScaleIAId);
        }

        [HideInInspector] [SerializeField] private uint setEntityScaleIAId;
        [LockstepInputAction(nameof(setEntityScaleIAId))]
        public void OnSetEntityScaleIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnSetEntityScaleIA");
            #endif
            uint entityId = lockstep.ReadSmallUInt();
            Vector3 scale = lockstep.ReadVector3();
            if (!TryGetEntityInstance(entityId, out Entity entity))
                return;
            entity.entityData.scale = scale;
            entity.ApplyScale();
        }

        public void SendDestroyEntityIA(uint entityId)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendDestroyEntityIA");
            #endif
            lockstep.WriteSmallUInt(entityId);
            lockstep.SendInputAction(destroyEntityIAId);
        }

        [HideInInspector] [SerializeField] private uint destroyEntityIAId;
        [LockstepInputAction(nameof(destroyEntityIAId))]
        public void OnDestroyEntityIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnDestroyEntityIA");
            #endif
            uint entityId = lockstep.ReadSmallUInt();
            if (TryGetEntityInstance(entityId, out Entity entity))
                DestroyEntity(entity);
        }

        public void SendDestroyAllEntitiesIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendDestroyAllEntitiesIA");
            #endif
            lockstep.SendInputAction(destroyAllEntitiesIAId);
        }

        [HideInInspector] [SerializeField] private uint destroyAllEntitiesIAId;
        [LockstepInputAction(nameof(destroyAllEntitiesIAId))]
        public void OnDestroyAllEntitiesIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnDestroyAllEntitiesIA");
            #endif
            DestroyAllEntities();
        }

        private void DestroyAllEntities()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyAllEntities");
            #endif
            for (int i = entityInstancesCount - 1; i >= 0 ; i--)
                DestroyEntity(entityInstances[i]);
        }

        private void DestroyNonPreInstantiatedEntities()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyNonPreInstantiatedEntities");
            #endif
            for (int i = entityInstancesCount - 1; i >= 0 ; i--)
            {
                Entity entity = entityInstances[i];
                if (!IsPreInstantiatedEntityId(entity.entityData.id, isImport: false))
                    DestroyEntity(entity);
            }
        }

        private void DestroyPreInstantiatedEntitiesWhichWereNotImported()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyPreInstantiatedEntitiesWhichWereNotImported");
            #endif
            for (int i = entityInstancesCount - 1; i >= 0 ; i--)
            {
                Entity entity = entityInstances[i];
                if (IsPreInstantiatedEntityId(entity.entityData.id, isImport: true) && entity.entityData.importedMetadata == null)
                    DestroyEntity(entity);
            }
        }

        public void DestroyEntity(Entity entity)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyEntity");
            #endif
            entityInstancesCount--;
            int instanceIndex = entity.instanceIndex;
            if (instanceIndex != entityInstancesCount)
            {
                entityInstances[instanceIndex] = entityInstances[entityInstancesCount];
                entityInstances[entityInstancesCount] = null; // Let GC clean up empty unity object reference objects.
                entityInstances[instanceIndex].instanceIndex = instanceIndex;
            }
            EntityData entityData = entity.entityData;
            entityInstancesById.Remove(entityData.id);

            Destroy(entity.gameObject);
            entityData.DecrementRefsCount();
        }

        public void WriteEntityExtensionReference(EntityExtension extension)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityExtensionReference");
            #endif
            lockstep.WriteSmallUInt(extension.entity.entityData.id);
            lockstep.WriteSmallUInt((uint)extension.extensionIndex);
        }

        public EntityExtension ReadEntityExtensionReferenceDynamic()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityExtensionReferenceDynamic");
            #endif
            uint entityId = lockstep.ReadSmallUInt();
            int index = (int)lockstep.ReadSmallUInt();
            if (!TryGetEntityInstance(entityId, out Entity entity))
                return null;
            return entity.extensions[index];
        }

        public ulong SendExtensionInputAction(EntityExtension extension, string methodName)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendExtensionInputAction");
            #endif
            byte[] buffer = new byte[10 + (5 + methodName.Length)]; // No multi byte characters, so this is fine.
            int bufferSize = 0;
            DataStream.WriteSmall(ref buffer, ref bufferSize, (uint)extension.entity.entityData.id);
            DataStream.WriteSmall(ref buffer, ref bufferSize, (uint)extension.extensionIndex);
            string className = extension.entity.prototype.ExtensionDataClassNames[extension.extensionIndex];
            int methodNameIndex = System.Array.IndexOf((string[])extensionMethodNamesLut[className].Reference, methodName);
            DataStream.WriteSmall(ref buffer, ref bufferSize, (uint)methodNameIndex);
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
            int methodNameIndex = (int)lockstep.ReadSmallUInt();
            string className = extension.entity.prototype.ExtensionDataClassNames[extension.extensionIndex];
            string methodName = ((string[])extensionMethodNamesLut[className].Reference)[methodNameIndex];
            extension.SendCustomEvent(methodName);
        }

        private void Export()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Export");
            #endif
            lockstep.WriteSmallUInt(highestPreInstantiatedEntityId);

            lockstep.WriteSmallUInt((uint)entityPrototypes.Length);
            foreach (EntityPrototype prototype in entityPrototypes)
                prototype.ExportMetadata();

            lockstep.WriteSmallUInt((uint)entityInstancesCount);
            for (int i = 0; i < entityInstancesCount; i++)
            {
                EntityData entityData = entityInstances[i].entityData;
                lockstep.WriteSmallUInt(entityData.id);
                lockstep.WriteSmallUInt(entityData.entityPrototype.Id);
            }
            for (int i = 0; i < entityInstancesCount; i++)
                entityInstances[i].entityData.Serialize(isExport: true);
        }

        private string Import(uint importedDataVersion)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Import");
            #endif
            importedPrototypeMetadataById = new DataDictionary();
            remappedImportedEntityData = new DataDictionary();

            highestImportedPreInstantiatedEntityId = lockstep.ReadSmallUInt();
            // There's technically no reason for this to happen separately to
            // DestroyPreInstantiatedEntitiesWhichWereNotImported, but this is easier to read.
            DestroyNonPreInstantiatedEntities();
            ReadAllImportedPrototypeMetadata();
            EntityData[] allImportedEntityData = ReadImportedIds();
            DestroyPreInstantiatedEntitiesWhichWereNotImported();
            ReadAndCreateImportedEntities(allImportedEntityData, importedDataVersion);
            DeleteEntityPrototypeMetadataClasses(allImportedEntityData);

            importedPrototypeMetadataById = null;
            remappedImportedEntityData = null;
            return null;
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

        private EntityData[] ReadImportedIds()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadImportedIds");
            #endif
            int count = (int)lockstep.ReadSmallUInt();
            EntityData[] allEntityData = new EntityData[count];
            for (int i = 0; i < count; i++)
            {
                uint id = lockstep.ReadSmallUInt();
                uint prototypeId = lockstep.ReadSmallUInt();
                EntityPrototypeMetadata metadata = GetImportedPrototypeMetadata(prototypeId);
                if (metadata.entityPrototype == null)
                    continue;
                EntityData entityData = (IsPreInstantiatedEntityId(id, isImport: true)
                    && TryGetEntityInstance(id, out Entity entity)
                    && entity.prototype == metadata.entityPrototype)
                    ? entity.entityData
                    : NewEntityData(nextEntityId++, metadata.entityPrototype);
                entityData.importedMetadata = metadata;
                allEntityData[i] = entityData;
                remappedImportedEntityData.Add(id, entityData);
            }
            return allEntityData;
        }

        private void ReadAndCreateImportedEntities(EntityData[] allImportedEntityData, uint importedDataVersion)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadAndCreateImportedEntities");
            #endif
            EntityData dummyEntityData = NewEntityData(0u, null);
            foreach (EntityData entityData in allImportedEntityData)
            {
                if (entityData == null)
                {
                    dummyEntityData.Deserialize(isImport: true, importedDataVersion);
                    continue;
                }
                entityData.Deserialize(isImport: true, importedDataVersion);
                if (entityData.entity != null)
                {
                    entityData.entity.ApplyEntityData();
                    continue;
                }
                Entity entity = InstantiateEntity(entityData.entityPrototype, entityData.id);
                entity.InitFromEntityData(entityData);
            }
            dummyEntityData.Delete();
        }

        private void DeleteEntityPrototypeMetadataClasses(EntityData[] allImportedEntityData)
        {
            DataList list = importedPrototypeMetadataById.GetValues();
            for (int i = 0; i < list.Count; i++)
                ((EntityPrototypeMetadata)list[i].Reference).Delete();
            foreach (EntityData entityData in allImportedEntityData)
                entityData.importedMetadata = null; // Clear reference to empty unity object reference object so that can get GCed too.
        }

        private void WriteEntityData(EntityData entityData, bool isExport)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityData - entityData.id: {entityData.id}, entityData.entityPrototype.Id: {entityData.entityPrototype.Id}");
            #endif
            lockstep.WriteSmallUInt(entityData.id);
            lockstep.WriteSmallUInt(entityData.entityPrototype.Id);
            entityData.Serialize(isExport);
        }

        private EntityData ReadEntityData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityData");
            #endif
            uint id = lockstep.ReadSmallUInt();
            uint prototypeId = lockstep.ReadSmallUInt();
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityData (inner) - id: {id}, prototypeId: {prototypeId}");
            #endif

            EntityPrototype prototype = GetEntityPrototype(prototypeId);

            if (!IsPreInstantiatedEntityId(id, isImport: false))
                return ReadEntityDataIntoNewEntity(prototype, id);

            if (TryGetEntityInstance(id, out Entity entity))
                return ReadEntityDataIntoExistingEntity(entity, id);

            Debug.LogError($"[EntitySystem] A pre instantiated entity was deleted before initialization "
                + $"in a non deterministic fashion. Prototype name: {prototype.PrototypeName}");
            return ReadEntityDataIntoNewEntity(prototype, id); // To recover, instantiate a new entity.
        }

        private EntityData ReadEntityDataIntoExistingEntity(Entity entity, uint id)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityDataIntoExistingEntity");
            #endif
            EntityData entityData = entity.entityData;
            if (entityData != null)
            {
                entityData.Deserialize(isImport: false, importedDataVersion: 0u);
                entity.ApplyEntityData();
                return entityData;
            }
            entityData = NewEntityData(id, entity.prototype);
            entityData.Deserialize(isImport: false, importedDataVersion: 0u);
            entity.InitFromEntityData(entityData);
            return entityData;
        }

        private EntityData ReadEntityDataIntoNewEntity(EntityPrototype prototype, uint id)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityDataIntoNewEntity");
            #endif
            EntityData entityData = NewEntityData(id, prototype);
            entityData.Deserialize(isImport: false, importedDataVersion: 0u);
            Entity entity = InstantiateEntity(prototype, id);
            entity.InitFromEntityData(entityData);
            return entityData;
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
                WriteEntityData(entityInstances[i].entityData, isExport);
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
                ReadEntityData();

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
