using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    public partial class EntitySystem : LockstepGameState
    {
        public override string GameStateInternalName => "jansharp.entity-system";
        public override string GameStateDisplayName => "Entity System";
        public override bool GameStateSupportsImportExport => true;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;
        public override LockstepGameStateOptionsUI ExportUI => null;
        public override LockstepGameStateOptionsUI ImportUI => null;

        private const long MaxWorkMSPerFrame = 20L;
        public const ulong InvalidUniqueId = 0uL;
        public const uint InvalidId = 0u;

        [HideInInspector][SerializeField][SingletonReference] private EntityPooling pooling;
        [HideInInspector][SerializeField][SingletonReference] private WannaBeClassesManager wannaBeClasses;

        [SerializeField] private EntityPrototype[] entityPrototypes;
        public EntityPrototype[] EntityPrototypes => entityPrototypes;
        private DataDictionary entityPrototypesById = new DataDictionary();
        private DataDictionary entityPrototypesByName = new DataDictionary();

        [SerializeField] private string[] rawExtensionMethodNamesLut;
        /// <summary>
        /// <para><see cref="string"/> className => <see cref="string[]"/> methodNames</para>
        /// </summary>
        private DataDictionary extensionDataMethodNamesLut = new DataDictionary();

        [BuildTimeIdAssignment(nameof(preInstantiatedEntityInstanceIds), nameof(highestPreInstantiatedEntityId))]
        [SerializeField] private Entity[] preInstantiatedEntityInstances;
        [SerializeField] private uint[] preInstantiatedEntityInstanceIds;
        [SerializeField] private EntityPrototype[] preInstantiatedEntityInstancePrototypes;
        [SerializeField] private EntityData[] preInstantiatedEntityData; // TODO: populate (including EntityExtensionData)
        /// <summary>
        /// <para>Only used for late joiner game state deserialization.</para>
        /// </summary>
        private DataDictionary preInstantiatedEntityIndexById;

        /// <summary>
        /// <para>Must be set by editor scripting, otherwise ids for pre instantiated entities could end up being reused.</para>
        /// </summary>
        [SerializeField] private uint highestPreInstantiatedEntityId;
        private uint highestImportedPreInstantiatedEntityId;
        private uint nextEntityId;

        /// <summary>
        /// <para><see cref="ulong"/> uniqueId => <see cref="EntityData"/> entityData</para>
        /// <para>Not game state safe.</para>
        /// </summary>
        private DataDictionary entityDataByUniqueId = new DataDictionary();
        /// <summary>
        /// <para><see cref="uint"/> id => <see cref="EntityData"/> entityData</para>
        /// <para>Game state safe.</para>
        /// </summary>
        private DataDictionary entityDataById = new DataDictionary();
        /// <summary><para>Game state safe.</para></summary>
        private EntityData[] allEntityData = new EntityData[ArrList.MinCapacity];
        /// <summary><para>Game state safe.</para></summary>
        private int allEntityDataCount = 0;

        private VRCPlayerApi localPlayer;
        private uint localPlayerId;

        private void Start()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Start");
#endif
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
            InitEntityPrototypes();
            InitExtensionIANameLut();
            nextEntityId = highestPreInstantiatedEntityId + 1u;
        }

        [LockstepEvent(LockstepEventType.OnInit)]
        public void OnInit()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnInit");
#endif
            InitPreInstantiatedEntities();
        }

        //         [LockstepEvent(LockstepEventType.OnClientBeginCatchUp)]
        //         public void OnClientBeginCatchUp()
        //         {
        // #if EntitySystemDebug
        //             Debug.Log($"[EntitySystemDebug] EntitySystem  OnClientBeginCatchUp");
        // #endif
        //         }

        private void InitEntityPrototypes()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitEntityPrototypes");
#endif
            foreach (EntityPrototype prototype in entityPrototypes)
            {
                entityPrototypesById.Add(prototype.Id, prototype);
                entityPrototypesByName.Add(prototype.PrototypeName, prototype);
                prototype.DefaultEntityInst.OnInstantiate(lockstep, this, wannaBeClasses, prototype, isDefaultInstance: true);
            }
        }

        private void InitPreInstantiatedEntities()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitPreInstantiatedEntities");
#endif
            int length = preInstantiatedEntityInstances.Length;
            for (int i = 0; i < length; i++)
                InitPreInstantiatedEntity(i);
            preInstantiatedEntityInstances = null;
            preInstantiatedEntityInstanceIds = null;
            preInstantiatedEntityInstancePrototypes = null;
            preInstantiatedEntityData = null;
        }

        private EntityData InitPreInstantiatedEntity(int index)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitPreInstantiatedEntity");
#endif
            Entity entity = preInstantiatedEntityInstances[index];
            uint id = preInstantiatedEntityInstanceIds[index];
            EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[index];
            EntityData entityData = preInstantiatedEntityData[index];
            entityData.WannaBeConstructor(prototype, InvalidUniqueId, id);
            entity.OnInstantiate(lockstep, this, wannaBeClasses, prototype, isDefaultInstance: false);
            RegisterEntityDataAndId(entityData);
            entityData.InitFromPreInstantiated(entity);
            entity.AssociateWithEntityData(entityData);
            return entityData;
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
                extensionDataMethodNamesLut.Add(className, new DataToken(methodNames));
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

        private void RegisterEntityDataAndId(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  RegisterEntityDataAndId");
#endif
            entityData.instanceIndex = allEntityDataCount;
            ArrList.Add(ref allEntityData, ref allEntityDataCount, entityData);
            entityDataById.Add(entityData.id, entityData);
        }

        private void DeregisterEntityDataAndId(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeregisterEntityDataAndId");
#endif
            allEntityDataCount--;
            int instanceIndex = entityData.instanceIndex;
            entityData.instanceIndex = -1;
            if (instanceIndex != allEntityDataCount)
            {
                EntityData entityDataTakingThePlace = allEntityData[allEntityDataCount];
                entityDataTakingThePlace.instanceIndex = instanceIndex;
                allEntityData[instanceIndex] = entityDataTakingThePlace;
                allEntityData[allEntityDataCount] = null; // Let GC clean up empty unity object reference objects.
            }
            entityDataById.Remove(entityData.id);
        }

        private EntityData NewEntityData(EntityPrototype prototype, ulong uniqueId, uint id)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  NewEntityData");
#endif
            EntityData entityData = wannaBeClasses.New<EntityData>(nameof(EntityData))
                .WannaBeConstructor(prototype, uniqueId, id);
            if (uniqueId != InvalidUniqueId)
                entityDataByUniqueId.Add(uniqueId, entityData);
            if (id != InvalidId) // Having an id means this is in a game state safe context.
                RegisterEntityDataAndId(entityData);
            return entityData;
        }

        private void SetEntityDataId(EntityData entityData, uint id)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetEntityPrototype");
#endif
            if (id == InvalidId)
            {
                Debug.LogError("[EntitySystem] Attempt to set entityData.id to 0u post creation, which is invalid.");
                return;
            }
            entityData.id = id;
            RegisterEntityDataAndId(entityData);
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

        public EntityData GetEntityData(uint entityId) => (EntityData)entityDataById[entityId].Reference;
        public bool TryGetEntityData(uint entityId, out EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetEntityInstance");
#endif
            if (entityDataById.TryGetValue(entityId, out DataToken token))
            {
                entityData = (EntityData)token.Reference;
                return true;
            }
            entityData = null;
            return false;
        }

        public EntityData SendCustomCreateEntityIA(uint iaId, uint prototypeId, Vector3 position, Quaternion rotation)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendCustomCreateEntityIA");
#endif
            if (!lockstep.IsInitialized)
                return null;

            lockstep.WriteSmallUInt(prototypeId);
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            ulong uniqueId = lockstep.SendInputAction(iaId);

            // Latency hiding.
            return CreateDefaultEntity(
                GetEntityPrototype(prototypeId), uniqueId, InvalidId,
                position, rotation,
                highPriority: true);
        }

        public EntityData ReadEntityInCustomCreateEntityIA(bool onEntityCreatedGetsRaisedLater = false)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityInCustomCreateEntityIA");
#endif
            uint prototypeId = lockstep.ReadSmallUInt();
            Vector3 position = lockstep.ReadVector3();
            Quaternion rotation = lockstep.ReadQuaternion();
            ulong uniqueId = lockstep.SendingUniqueId;
            uint id = nextEntityId++;

            EntityData entityData;
            if (lockstep.SendingPlayerId == localPlayerId) // Latency hiding
            {
                entityData = (EntityData)entityDataByUniqueId[uniqueId].Reference;
                SetEntityDataId(entityData, id);
            }
            else
                entityData = CreateDefaultEntity(
                    GetEntityPrototype(prototypeId), uniqueId, id,
                    position, rotation,
                    highPriority: false);

            entityData.OnEntityDataCreated();
            if (!onEntityCreatedGetsRaisedLater)
                RaiseOnEntityCreated(entityData);
            return entityData;
        }

        public void RaiseOnEntityCreatedInCustomCreateEntityIA(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityCreatedInCustomCreateEntityIA");
#endif
            RaiseOnEntityCreated(entityData);
        }

        public EntityData SendCreateEntityIA(uint prototypeId, Vector3 position, Quaternion rotation)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendCreateEntityIA");
#endif
            return SendCustomCreateEntityIA(createEntityIAId, prototypeId, position, rotation);
        }

        [HideInInspector][SerializeField] private uint createEntityIAId;
        [LockstepInputAction(nameof(createEntityIAId))]
        public void OnCreateEntityIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnCreateEntityIA");
#endif
            ReadEntityInCustomCreateEntityIA();
        }

        private EntityData CreateDefaultEntity(
            EntityPrototype prototype,
            ulong uniqueId,
            uint id,
            Vector3 position,
            Quaternion rotation,
            bool highPriority)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateDefaultEntity");
#endif
            EntityData entityData = NewEntityData(prototype, uniqueId, id);
            entityData.InitFromDefault(position, rotation, prototype.DefaultScale);
            pooling.RequestEntity(entityData, highPriority);
            return entityData;
        }

        public void WriteEntityDataRef(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityDataRef");
#endif
            uint id = entityData.id;
            if (id != InvalidId)
            {
                lockstep.WriteSmallUInt(id);
                return;
            }
            ulong uniqueId = entityData.uniqueId;
            if (uniqueId == InvalidUniqueId)
            {
                Debug.LogError($"[EntitySystem] Attempt to WriteEntityDataReference where both id and uniqueId are invalid.");
                return;
            }
            lockstep.WriteByte(0xff); // WriteSmall never writes 0xff as its first byte.
            lockstep.WriteULong(uniqueId);
        }

        public bool TryReadEntityDataRef(out EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryReadEntityDataRef");
#endif
            byte header = lockstep.ReadByte();
            if (header != 0xff)
            {
                lockstep.ReadStreamPosition--;
                uint id = lockstep.ReadSmallUInt();
                return TryGetEntityData(id, out entityData);
            }
            ulong uniqueId = lockstep.ReadULong();
            if (entityDataByUniqueId.TryGetValue(uniqueId, out DataToken entityDataToken))
            {
                entityData = (EntityData)entityDataToken.Reference;
                return true;
            }
            entityData = null;
            return false;
        }

        public ulong SendTransformChangeIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendTransformChangeIA");
#endif
            return lockstep.SendInputAction(transformChangeIAId);
        }

        [HideInInspector][SerializeField] private uint transformChangeIAId;
        [LockstepInputAction(nameof(transformChangeIAId))]
        public void OnTransformChangeIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnTransformChangeIA");
#endif
            if (!TryReadEntityDataRef(out EntityData entityData))
                return;
            entityData.OnTransformChangeIA();
        }

        public void SendDestroyEntityIA(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendDestroyEntityIA");
#endif
            if (entityData.entityIsDestroyed)
                return;
            WriteEntityDataRef(entityData);
            lockstep.SendInputAction(destroyEntityIAId);
            pooling.ReturnEntity(entityData); // Latency hiding.
        }

        [HideInInspector][SerializeField] private uint destroyEntityIAId;
        [LockstepInputAction(nameof(destroyEntityIAId))]
        public void OnDestroyEntityIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnDestroyEntityIA");
#endif
            if (TryReadEntityDataRef(out EntityData entityData))
                DestroyEntity(entityData);
        }

        public void SendDestroyAllEntitiesIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendDestroyAllEntitiesIA");
#endif
            lockstep.SendInputAction(destroyAllEntitiesIAId);
        }

        [HideInInspector][SerializeField] private uint destroyAllEntitiesIAId;
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
            for (int i = allEntityDataCount - 1; i >= 0; i--)
                DestroyEntity(allEntityData[i]);
        }

        private int destroyNonPreInstantiatedEntitiesIndex = -1;
        private void DestroyNonPreInstantiatedEntities()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyNonPreInstantiatedEntities");
#endif
            bool isImporting = lockstep.IsDeserializingForImport;
            int startIndex = destroyNonPreInstantiatedEntitiesIndex == -1
                ? allEntityDataCount - 1
                : destroyNonPreInstantiatedEntitiesIndex - 1;
            for (int i = startIndex; i >= 0; i--)
            {
                EntityData entityData = allEntityData[i];
                if (IsPreInstantiatedEntityId(entityData.id, isImport: false))
                    continue;
                DestroyEntity(entityData);
                if (isImporting && DeserializationIsRunningLong())
                {
                    destroyNonPreInstantiatedEntitiesIndex = i;
                    return;
                }
            }
            destroyNonPreInstantiatedEntitiesIndex = -1;
            if (isImporting)
                deserializationStage++;
        }

        private int destroyEntitiesWhichWereNotImportedIndex = -1;
        private void DestroyEntitiesWhichWereNotImported()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyEntitiesWhichWereNotImported");
#endif
            int startIndex = destroyEntitiesWhichWereNotImportedIndex == -1
                ? allEntityDataCount - 1
                : destroyEntitiesWhichWereNotImportedIndex - 1;
            for (int i = startIndex; i >= 0; i--)
            {
                EntityData entityData = allEntityData[i];
                if (entityData.importedMetadata != null)
                    continue;
                DestroyEntity(entityData);
                if (DeserializationIsRunningLong())
                {
                    destroyEntitiesWhichWereNotImportedIndex = i;
                    return;
                }
            }
            destroyEntitiesWhichWereNotImportedIndex = -1;
            deserializationStage++;
        }

        public void DestroyEntity(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyEntity");
#endif
            if (entityData.instanceIndex == -1)
                return;
            pooling.ReturnEntity(entityData);
            if (entityData.uniqueId != InvalidUniqueId)
                entityDataByUniqueId.Remove(entityData.uniqueId);
            DeregisterEntityDataAndId(entityData);
            entityData.OnEntityDataDestroyed();
            RaiseOnEntityDestroyed(entityData);
            entityData.DecrementRefsCount();
        }

        public void WriteEntityExtensionDataRef(EntityExtensionData extensionData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityExtensionDataRef");
#endif
            lockstep.WriteSmallUInt(extensionData.entityData.id);
            lockstep.WriteSmallUInt((uint)extensionData.extensionIndex);
        }

        public EntityExtensionData ReadEntityExtensionDataRefDynamic()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityExtensionDataRefDynamic");
#endif
            uint entityId = lockstep.ReadSmallUInt();
            int extensionIndex = (int)lockstep.ReadSmallUInt();
            if (!TryGetEntityData(entityId, out EntityData entityData))
                return null;
            return entityData.allExtensionData[extensionIndex];
        }

        private byte[] sendExtensionDataInputActionBuffer = new byte[5 * 3]; // Max size of 3 SmallUInt.
        public ulong SendExtensionDataInputAction(EntityExtensionData extensionData, string methodName)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendExtensionDataInputAction");
#endif
            int bufferSize = 0;
            DataStream.WriteSmall(ref sendExtensionDataInputActionBuffer, ref bufferSize, extensionData.entityData.id);
            DataStream.WriteSmall(ref sendExtensionDataInputActionBuffer, ref bufferSize, (uint)extensionData.extensionIndex);
            string className = extensionData.entityData.entityPrototype.ExtensionDataClassNames[extensionData.extensionIndex];
            int methodNameIndex = System.Array.IndexOf((string[])extensionDataMethodNamesLut[className].Reference, methodName);
            if (methodNameIndex == -1)
            {
                Debug.LogError($"[EntitySystem] Attempt to SendExtensionDataInputAction with the method name "
                    + $"{methodName} on the class {className}, however no such method has the "
                    + $"[{nameof(EntityExtensionDataInputActionAttribute)}].");
                lockstep.ResetWriteStream();
                return 0uL;
            }
            DataStream.WriteSmall(ref sendExtensionDataInputActionBuffer, ref bufferSize, (uint)methodNameIndex);
            int iaSize = lockstep.WriteStreamPosition;
            lockstep.ShiftWriteStream(0, bufferSize, iaSize);
            lockstep.WriteStreamPosition = 0;
            lockstep.WriteBytes(sendExtensionDataInputActionBuffer, 0, bufferSize);
            lockstep.WriteStreamPosition = bufferSize + iaSize;
            return lockstep.SendInputAction(onExtensionInputActionIAId);
        }

        [HideInInspector][SerializeField] private uint onExtensionInputActionIAId;
        [LockstepInputAction(nameof(onExtensionInputActionIAId))]
        public void OnExtensionInputActionIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnExtensionInputActionIA");
#endif
            EntityExtensionData extensionData = ReadEntityExtensionDataRefDynamic();
            if (extensionData == null)
                return;
            int methodNameIndex = (int)lockstep.ReadSmallUInt();
            string className = extensionData.entityData.entityPrototype.ExtensionDataClassNames[extensionData.extensionIndex];
            string methodName = ((string[])extensionDataMethodNamesLut[className].Reference)[methodNameIndex];
            extensionData.SendCustomEvent(methodName);
        }

        private int exportStage = 0;
        private int suspendedIndexInArray = 0;

        private void Export()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Export");
#endif
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            if (exportStage == 0)
            {
                lockstep.WriteSmallUInt(highestPreInstantiatedEntityId);

                lockstep.WriteSmallUInt((uint)entityPrototypes.Length);
                foreach (EntityPrototype prototype in entityPrototypes)
                    prototype.ExportMetadata();

                lockstep.WriteSmallUInt((uint)allEntityDataCount);
                exportStage++;
            }

            if (exportStage == 1)
            {
                for (int i = suspendedIndexInArray; i < allEntityDataCount; i++)
                {
                    EntityData entityData = allEntityData[i];
                    lockstep.WriteSmallUInt(entityData.id);
                    lockstep.WriteSmallUInt(entityData.entityPrototype.Id);
                    if (sw.ElapsedMilliseconds > MaxWorkMSPerFrame)
                    {
                        suspendedIndexInArray = i + 1;
                        lockstep.FlagToContinueNextFrame();
                        return;
                    }
                }
                suspendedIndexInArray = 0;
                exportStage++;
            }

            if (exportStage == 2)
            {
                for (int i = suspendedIndexInArray; i < allEntityDataCount; i++)
                {
                    allEntityData[i].Serialize(isExport: true);
                    if (sw.ElapsedMilliseconds > MaxWorkMSPerFrame)
                    {
                        suspendedIndexInArray = i + 1;
                        lockstep.FlagToContinueNextFrame();
                        return;
                    }
                }
                suspendedIndexInArray = 0;
                exportStage = 0;
            }
        }

        private int deserializationStage = 0;
        private System.Diagnostics.Stopwatch deserializationSw = new System.Diagnostics.Stopwatch();

        private bool DeserializationIsRunningLong()
        {
            bool result = deserializationSw.ElapsedMilliseconds > MaxWorkMSPerFrame;
            if (result)
                lockstep.FlagToContinueNextFrame();
            return result;
        }

        private EntityData[] allImportedEntityData = null;
        private string Import(uint importedDataVersion)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Import - deserializationStage: {deserializationStage}");
#endif
            if (deserializationStage == 0)
            {
                importedPrototypeMetadataById = new DataDictionary();
                remappedImportedEntityData = new DataDictionary();
                highestImportedPreInstantiatedEntityId = lockstep.ReadSmallUInt();
                deserializationStage++;
            }
            // There's technically no reason for this to happen separately to
            // DestroyEntitiesWhichWereNotImported, but this is easier to read.
            if (deserializationStage == 1)
                DestroyNonPreInstantiatedEntities();
            if (deserializationStage == 2)
                ReadAllImportedPrototypeMetadata();
            if (deserializationStage == 3)
                allImportedEntityData = ReadImportedIds();
            if (deserializationStage == 4)
                DestroyEntitiesWhichWereNotImported();
            if (deserializationStage == 5)
                ReadAndCreateImportedEntities(allImportedEntityData, importedDataVersion);
            if (deserializationStage == 6)
                DeleteEntityPrototypeMetadataClasses(allImportedEntityData);
            if (deserializationStage == 7)
            {
                deserializationStage = 0;
                allImportedEntityData = null;
                importedPrototypeMetadataById = null;
                remappedImportedEntityData = null;
            }
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

        private int readAllImportedPrototypeMetadataLength = -1;
        private int readAllImportedPrototypeMetadataIndex = 0;
        private void ReadAllImportedPrototypeMetadata()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadAllImportedPrototypeMetadata");
#endif
            if (readAllImportedPrototypeMetadataLength == -1)
                readAllImportedPrototypeMetadataLength = (int)lockstep.ReadSmallUInt();
            for (int i = readAllImportedPrototypeMetadataIndex; i < readAllImportedPrototypeMetadataLength; i++)
            {
                EntityPrototypeMetadata metadata = EntityPrototypeStatics.ImportMetadata(wannaBeClasses, lockstep, this);
                importedPrototypeMetadataById.Add(metadata.id, metadata);
                if (DeserializationIsRunningLong())
                {
                    readAllImportedPrototypeMetadataIndex = i + 1;
                    return;
                }
            }
            readAllImportedPrototypeMetadataLength = -1;
            readAllImportedPrototypeMetadataIndex = 0;
            deserializationStage++;
        }

        private int readImportedIdsIndex = 0;
        private EntityData[] readImportedIdsAllEntityData = null;
        private EntityData[] ReadImportedIds()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadImportedIds");
#endif
            if (readImportedIdsAllEntityData == null)
                readImportedIdsAllEntityData = new EntityData[(int)lockstep.ReadSmallUInt()];
            int length = readImportedIdsAllEntityData.Length;
            for (int i = readImportedIdsIndex; i < length; i++)
            {
                uint id = lockstep.ReadSmallUInt();
                uint prototypeId = lockstep.ReadSmallUInt();
                EntityPrototypeMetadata metadata = GetImportedPrototypeMetadata(prototypeId);
                if (metadata.entityPrototype == null)
                    continue;
                EntityData entityData = (IsPreInstantiatedEntityId(id, isImport: true)
                    && TryGetEntityData(id, out EntityData existingEntityData)
                    && existingEntityData.entityPrototype == metadata.entityPrototype)
                    ? existingEntityData
                    : NewEntityData(metadata.entityPrototype, InvalidUniqueId, nextEntityId++);
                entityData.importedMetadata = metadata;
                readImportedIdsAllEntityData[i] = entityData;
                remappedImportedEntityData.Add(id, entityData);
                if (DeserializationIsRunningLong())
                {
                    readImportedIdsIndex = i + 1;
                    return null;
                }
            }
            EntityData[] result = readImportedIdsAllEntityData;
            readImportedIdsIndex = 0;
            readImportedIdsAllEntityData = null;
            deserializationStage++;
            return result;
        }

        private int readAndCreateImportedEntitiesIndex = -1;
        private EntityData readAndCreateImportedEntitiesDummy = null;
        private void ReadAndCreateImportedEntities(EntityData[] allImportedEntityData, uint importedDataVersion)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadAndCreateImportedEntities");
#endif
            int length = allImportedEntityData.Length;
            int startIndex;
            if (readAndCreateImportedEntitiesIndex != -1)
                startIndex = readAndCreateImportedEntitiesIndex;
            else
            {
                startIndex = 0;
                readAndCreateImportedEntitiesDummy = NewEntityData(prototype: null, InvalidUniqueId, InvalidId);
            }
            for (int i = startIndex; i < length; i++)
            {
                if (DeserializationIsRunningLong())
                {
                    readAndCreateImportedEntitiesIndex = i;
                    return;
                }
                EntityData entityData = allImportedEntityData[i];
                if (entityData == null)
                {
                    readAndCreateImportedEntitiesDummy.Deserialize(isImport: true, importedDataVersion);
                    continue;
                }
                entityData.Deserialize(isImport: true, importedDataVersion);
                if (entityData.entity != null)
                    entityData.entity.ApplyEntityData();
                else
                    pooling.RequestEntity(entityData);
            }
            readAndCreateImportedEntitiesIndex = -1;
            readAndCreateImportedEntitiesDummy.Delete();
            readAndCreateImportedEntitiesDummy = null;
            deserializationStage++;
        }

        private void DeleteEntityPrototypeMetadataClasses(EntityData[] allImportedEntityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeleteEntityPrototypeMetadataClasses");
#endif
            DataList list = importedPrototypeMetadataById.GetValues();
            for (int i = 0; i < list.Count; i++)
                ((EntityPrototypeMetadata)list[i].Reference).Delete();
            foreach (EntityData entityData in allImportedEntityData)
                if (entityData != null)
                    entityData.importedMetadata = null; // Clear reference to empty unity object reference object so that can get GCed too.
            deserializationStage++;
        }

        private void WriteEntityData(EntityData entityData, bool isExport)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityData - entityData.id: {entityData.id}, entityData.entityPrototype.Id: {entityData.entityPrototype.Id}");
#endif
            lockstep.WriteSmallUInt(entityData.id);
            if (!IsPreInstantiatedEntityId(entityData.id, isImport: false))
                lockstep.WriteULong(entityData.uniqueId);
            lockstep.WriteSmallUInt(entityData.entityPrototype.Id);
            entityData.Serialize(isExport);
        }

        private EntityData ReadEntityData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityData");
#endif
            uint id = lockstep.ReadSmallUInt();
            bool isPreInstantiated = IsPreInstantiatedEntityId(id, isImport: false);
            ulong uniqueId = isPreInstantiated ? InvalidUniqueId : lockstep.ReadULong();
            uint prototypeId = lockstep.ReadSmallUInt();
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityData (inner) - id: {id}, uniqueId: 0x{uniqueId:x16}, prototypeId: {prototypeId}");
#endif
            if (!isPreInstantiated)
                return ReadEntityDataIntoNewEntity(GetEntityPrototype(prototypeId), uniqueId, id);
            return DeserializePreInstantiatedEntity(id, prototypeId);
        }

        private EntityData ReadEntityDataIntoNewEntity(EntityPrototype prototype, ulong uniqueId, uint id)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityDataIntoNewEntity");
#endif
            EntityData entityData = NewEntityData(prototype, uniqueId, id);
            entityData.Deserialize(isImport: false, importedDataVersion: 0u);
            pooling.RequestEntity(entityData);
            return entityData;
        }

        private EntityData DeserializePreInstantiatedEntity(uint id, uint prototypeId)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializePreInstantiatedEntity");
#endif
            if (!preInstantiatedEntityIndexById.TryGetValue(id, out DataToken indexToken))
            {
                Debug.LogError($"[EntitySystem] Impossible, the incoming/deserialized id {id} should be an id of "
                    + $"a pre instantiated entity, however there is no pre instantiated entity with this id.");
                // Instantiate a new entity to recover.
                return ReadEntityDataIntoNewEntity(GetEntityPrototype(prototypeId), InvalidId, id);
            }
            int index = indexToken.Int;
            preInstantiatedEntityInstanceIds[index] = InvalidId; // Mark as used.

            EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[index];
            EntityData entityData = preInstantiatedEntityData[index];
            entityData.WannaBeConstructor(prototype, InvalidUniqueId, id);
            RegisterEntityDataAndId(entityData);
            entityData.Deserialize(isImport: false, importedDataVersion: 0u);

            Entity entity = preInstantiatedEntityInstances[index];
            if (entity != null)
            {
                entity.OnInstantiate(lockstep, this, wannaBeClasses, prototype, isDefaultInstance: false);
                entity.AssociateWithEntityData(entityData);
                return entityData;
            }

            Debug.LogError($"[EntitySystem] A pre instantiated entity was deleted before initialization "
                + $"in a non deterministic fashion. Prototype name: {prototype.PrototypeName}");
            pooling.RequestEntity(entityData); // To recover, instantiate a new entity.
            return entityData;
        }

        private int entitiesToWriteIndex;

        public override void SerializeGameState(bool isExport, LockstepGameStateOptionsData exportOptions)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeGameState");
#endif
            if (isExport)
            {
                Export();
                return;
            }

            if (!lockstep.IsContinuationFromPrevFrame)
            {
                lockstep.WriteSmallUInt(nextEntityId);
                lockstep.WriteSmallUInt((uint)allEntityDataCount);
                entitiesToWriteIndex = 0;
            }
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            while (entitiesToWriteIndex < allEntityDataCount)
            {
                if (sw.ElapsedMilliseconds > MaxWorkMSPerFrame)
                {
                    lockstep.FlagToContinueNextFrame();
                    return;
                }
                WriteEntityData(allEntityData[entitiesToWriteIndex], isExport: false);
                entitiesToWriteIndex++;
            }
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion, LockstepGameStateOptionsData importOptions)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeGameState");
#endif
            deserializationSw.Reset();
            deserializationSw.Start();

            if (isImport)
                return Import(importedDataVersion);

            if (deserializationStage == 0)
            {
                // Build lut for use in late joiner deserialization.
                preInstantiatedEntityIndexById = new DataDictionary();
                for (int i = 0; i < preInstantiatedEntityInstanceIds.Length; i++)
                    preInstantiatedEntityIndexById.Add(preInstantiatedEntityInstanceIds[i], i);
                nextEntityId = lockstep.ReadSmallUInt();
                deserializationStage++;
            }
            if (deserializationStage == 1)
                DeserializeEntities();
            if (deserializationStage == 2)
                DestroyUnusedPreInstantiatedEntities();
            if (deserializationStage == 3)
                deserializationStage = 0;
            return null;
        }

        private int deserializeEntitiesCount = -1;
        private int deserializeEntitiesIndex = -1;
        private void DeserializeEntities()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeEntities");
#endif
            if (deserializeEntitiesCount == -1)
            {
                deserializeEntitiesCount = (int)lockstep.ReadSmallUInt();
                deserializeEntitiesIndex = 0;
                ArrList.EnsureCapacity(ref allEntityData, deserializeEntitiesCount);
            }
            while (deserializeEntitiesIndex < deserializeEntitiesCount)
            {
                EntityData entityData = ReadEntityData();
                RaiseOnEntityDeserialized(entityData);
                deserializeEntitiesIndex++;
                if (DeserializationIsRunningLong())
                    return;
            }
            deserializeEntitiesCount = -1;
            deserializeEntitiesIndex = -1;
            deserializationStage++;
        }

        private int destroyUnusedPreInstantiatedEntitiesIndex = 0;
        private void DestroyUnusedPreInstantiatedEntities()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyUnusedPreInstantiatedEntities");
#endif
            int length = preInstantiatedEntityInstanceIds.Length;
            while (destroyUnusedPreInstantiatedEntitiesIndex < length)
            {
                if (preInstantiatedEntityInstanceIds[destroyUnusedPreInstantiatedEntitiesIndex] == InvalidId)
                {
                    destroyUnusedPreInstantiatedEntitiesIndex++;
                    continue;
                }
                // This makes both entities and entityData go through their usual lifecycle, which ultimately
                // allows for the entity instances to be put into the entity pool rather than just being
                // destroyed right now.
                EntityData entityData = InitPreInstantiatedEntity(destroyUnusedPreInstantiatedEntitiesIndex);
                DestroyEntity(entityData);
                destroyUnusedPreInstantiatedEntitiesIndex++;
                if (DeserializationIsRunningLong())
                    return;
            }
            preInstantiatedEntityInstances = null;
            preInstantiatedEntityInstanceIds = null;
            preInstantiatedEntityInstancePrototypes = null;
            preInstantiatedEntityData = null;
            preInstantiatedEntityIndexById = null;
            destroyUnusedPreInstantiatedEntitiesIndex = 0;
            deserializationStage++;
        }
    }

    public static class EntitySystemExtension
    {
        public static T ReadEntityExtensionDataRef<T>(this EntitySystem entitySystem)
            where T : EntityExtensionData
        {
            return (T)entitySystem.ReadEntityExtensionDataRefDynamic();
        }

        // public static T GetExtensionData<T>(
        //     this EntitySystem _,
        //     EntityData entityData,
        //     string extensionDataClassName,
        //     int startIndex = 0)
        //     where T : EntityExtensionData
        // {
        //     // Copy pasted for performance.
        //     int extensionIndex = System.Array.IndexOf(entityData.entityPrototype.ExtensionDataClassNames, extensionDataClassName, startIndex);
        //     return (T)(extensionIndex < 0 ? null : entityData.allExtensionData[extensionIndex]);
        // }
    }
}
