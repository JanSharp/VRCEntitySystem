using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    [LockstepGameStateDependency(typeof(EntitySystemOptionsGS))]
    [LockstepGameStateDependency(typeof(PlayerDataManagerAPI))]
    [DefaultExecutionOrder(-100)]
    [CustomRaisedEventsDispatcher(typeof(EntitySystemEventAttribute), typeof(EntitySystemEventType))]
    public class EntitySystem : EntitySystemAPI
    {
        public override string GameStateInternalName => "jansharp.entity-system";
        public override string GameStateDisplayName => "Entity System";
        public override bool GameStateSupportsImportExport => true;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;
        public override LockstepGameStateOptionsUI ExportUI => null;
        public override LockstepGameStateOptionsUI ImportUI => null;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public const string RawExtensionMethodNamesLutPropName = nameof(rawExtensionMethodNamesLut);
        public const string EntityPrototypesPropName = nameof(entityPrototypes);
        public const string PreInstantiatedEntityInstancesPropName = nameof(preInstantiatedEntityInstances);
        public const string PreInstantiatedEntityInstancePrototypesPropName = nameof(preInstantiatedEntityInstancePrototypes);
        public const string PreInstantiatedEntityDataPropName = nameof(preInstantiatedEntityData);
#endif

        private double maxWorkMSPerFrame;

        [HideInInspector][SerializeField][SingletonReference] private EntityPooling pooling;
        [HideInInspector][SerializeField][SingletonReference] private EntityDataLogic entityDataLogic;
        [HideInInspector][SerializeField][SingletonReference] private EntityLogic entityLogic;
        [HideInInspector][SerializeField][SingletonReference] private WannaBeClassesManager wannaBeClasses;
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataManagerAPI playerDataManager;

        [SerializeField] private EntitySystemOptionsGS optionsGS;
        public override EntitySystemImportExportOptions ExportOptions => (EntitySystemImportExportOptions)optionsGS.OptionsForCurrentExport;
        public override EntitySystemImportExportOptions ImportOptions => (EntitySystemImportExportOptions)optionsGS.OptionsForCurrentImport;
        public override EntitySystemImportExportOptions OptionsFromExport => optionsGS.OptionsFromExport;

        [SerializeField] private Transform preInstantiatedEntityDataContainer;
        [SerializeField] private Transform entityPrefabInstsContainer;
        [SerializeField] private Transform defaultEntityInstsContainer;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public Transform PreInstantiatedEntityDataContainer => preInstantiatedEntityDataContainer;
        public Transform EntityPrefabInstsContainer => entityPrefabInstsContainer;
        public Transform DefaultEntityInstsContainer => defaultEntityInstsContainer;
#endif

        [SerializeField] private EntityPrototype[] entityPrototypes;
        public override EntityPrototype[] EntityPrototypes => entityPrototypes;
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
        /// <summary>
        /// <para>These also already have their <see cref="EntityData.allExtensionData"/> populated at build
        /// time.</para>
        /// </summary>
        [SerializeField] private EntityData[] preInstantiatedEntityData;
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
        /// <para>Must not be cleared entirely by imports, see
        /// <see cref="ReadEntityInCustomCreateEntityIA"/>.</para>
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

        private int suspendedIndexInArray = 0;
        private System.Diagnostics.Stopwatch workSw = new System.Diagnostics.Stopwatch();
        private bool WorkIsRunningLong()
        {
            bool result = workSw.Elapsed.TotalMilliseconds > maxWorkMSPerFrame;
            if (result)
                lockstep.FlagToContinueNextFrame();
            return result;
        }
        private EntityData[] allEntityDataSorted = null;
        private int suspendedAllEntityDataSortedCount = 0;
        private int suspendedIndexInAllEntityDataSorted = 0;
        private int deSerializationStage = 0;
        #region Deserializing
        private int entitiesToDeserializeCount;
        #endregion
        #region Importing
        private EntityData[] allImportedEntityData = null;
        private DataDictionary importedPrototypeMetadataById = null;
        private DataDictionary remappedImportedEntityData = null;
        private EntityData dummyEntityDataForImport = null;
        #endregion

        private VRCPlayerApi localPlayer;
        private uint localPlayerId;
        private int playerDataClassNameIndex;
        public override int PlayerDataClassNameIndex => playerDataClassNameIndex;

        public override EntitySystemPlayerData GetPlayerDataForPlayerId(uint playerId)
        {
            CorePlayerData core = playerDataManager.GetCorePlayerDataForPlayerId(playerId);
            return core == null ? null : (EntitySystemPlayerData)core.customPlayerData[playerDataClassNameIndex];
        }

        public override EntitySystemPlayerData GetPlayerDataForPersistentId(uint persistentId)
        {
            CorePlayerData core = playerDataManager.GetCorePlayerDataForPersistentId(persistentId);
            return core == null ? null : (EntitySystemPlayerData)core.customPlayerData[playerDataClassNameIndex];
        }

        public override EntitySystemPlayerData GetPlayerData(CorePlayerData core)
        {
            return (EntitySystemPlayerData)core.customPlayerData[playerDataClassNameIndex];
        }

        public override void WritePlayerDataRef(EntitySystemPlayerData playerData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WritePlayerDataRef");
#endif
            playerDataManager.WriteCorePlayerDataRef(playerData == null ? null : playerData.core);
        }

        public override EntitySystemPlayerData ReadPlayerDataRef(bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadPlayerDataRef");
#endif
            CorePlayerData core = playerDataManager.ReadCorePlayerDataRef(isImport);
            return core == null ? null : (EntitySystemPlayerData)core.customPlayerData[playerDataClassNameIndex];
        }

        private void Start()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  Start");
#endif
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
            maxWorkMSPerFrame = lockstep.MaxWorkMSPerFrame;
            InitEntityPrototypes();
            InitExtensionIANameLut();
            nextEntityId = highestPreInstantiatedEntityId + 1u;
            RunOnInstantiateForAllPreInstantiatedEntities();
        }

        [LockstepEvent(LockstepEventType.OnMaxWorkMSPerFrameChanged)]
        public void OnMaxWorkMSPerFrameChanged()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnMaxWorkMSPerFrameChanged");
#endif
            maxWorkMSPerFrame = lockstep.MaxWorkMSPerFrame;
        }

        [PlayerDataEvent(PlayerDataEventType.OnRegisterCustomPlayerData)]
        public void OnRegisterCustomPlayerData()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnRegisterCustomPlayerData");
#endif
            playerDataManager.RegisterCustomPlayerData<EntitySystemPlayerData>(nameof(EntitySystemPlayerData));
        }

        [PlayerDataEvent(PlayerDataEventType.OnAllCustomPlayerDataRegistered)]
        public void OnAllCustomPlayerDataRegistered()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnAllCustomPlayerDataRegistered");
#endif
            playerDataClassNameIndex = playerDataManager.GetPlayerDataClassNameIndex<EntitySystemPlayerData>(nameof(EntitySystemPlayerData));
        }

        // Must run after PlayerDataManager OnInit, but should run before any regular Order 0 handlers.
        [LockstepEvent(LockstepEventType.OnInit, Order = -5000)]
        public void OnInit()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnInit");
#endif
            InitPreInstantiatedEntities();
        }

        private void InitEntityPrototypes()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitEntityPrototypes");
#endif
            foreach (EntityPrototype prototype in entityPrototypes)
            {
                entityPrototypesById.Add(prototype.Id, prototype);
                entityPrototypesByName.Add(prototype.PrototypeName, prototype);
                entityLogic.OnInstantiate(prototype.DefaultEntityInst, prototype, isDefaultInstance: true);
            }
        }

        private void RunOnInstantiateForAllPreInstantiatedEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RunOnInstantiateForAllPreInstantiatedEntities");
#endif
            int length = preInstantiatedEntityInstances.Length;
            for (int i = 0; i < length; i++)
            {
                Entity entity = preInstantiatedEntityInstances[i];
                EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[i];
                entityLogic.OnInstantiate(entity, prototype, isDefaultInstance: false);
            }
        }

        private void InitPreInstantiatedEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitPreInstantiatedEntities");
#endif
            workSw.Restart(); // No de/serialization can run while this is running so just reuse that logic.
            int length = preInstantiatedEntityInstances.Length;
            while (suspendedIndexInArray < length)
            {
                if (WorkIsRunningLong())
                    return;
                InitPreInstantiatedEntity(suspendedIndexInArray++);
            }
            suspendedIndexInArray = 0;
            preInstantiatedEntityInstances = null;
            preInstantiatedEntityInstanceIds = null;
            preInstantiatedEntityInstancePrototypes = null;
            preInstantiatedEntityData = null;
        }

        private EntityData InitPreInstantiatedEntity(int index)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitPreInstantiatedEntity");
#endif
            Entity entity = preInstantiatedEntityInstances[index];
            uint id = preInstantiatedEntityInstanceIds[index];
            EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[index];
            EntityData entityData = preInstantiatedEntityData[index];
            entityDataLogic.WannaBeConstructor(entityData, prototype, InvalidUniqueId, id);
            RegisterEntityDataAndId(entityData);
            entityDataLogic.InitFromPreInstantiated(entityData, entity);
            entityLogic.AssociateWithEntityData(entity, entityData);
            return entityData;
        }

        private void InitExtensionIANameLut()
        {
#if ENTITY_SYSTEM_DEBUG
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

        public override bool IsPreInstantiatedEntityId(uint id, bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  IsPreInstantiatedEntityId - id: {id}");
#endif
            return id <= highestPreInstantiatedEntityId
                && (!isImport || id <= highestImportedPreInstantiatedEntityId);
        }

        private void RegisterEntityDataAndId(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RegisterEntityDataAndId");
#endif
            entityData.instanceIndex = allEntityDataCount;
            ArrList.Add(ref allEntityData, ref allEntityDataCount, entityData);
            entityDataById.Add(entityData.id, entityData);
        }

        private void DeregisterEntityDataAndId(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
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
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  NewEntityData");
#endif
            EntityData entityData = wannaBeClasses.New<EntityData>(nameof(EntityData));
            entityDataLogic.WannaBeConstructor(entityData, prototype, uniqueId, id);
            if (uniqueId != InvalidUniqueId)
                entityDataByUniqueId.Add(uniqueId, entityData);
            if (id != InvalidId) // Having an id means this is in a game state safe context.
                RegisterEntityDataAndId(entityData);
            return entityData;
        }

        private void SetEntityDataId(EntityData entityData, uint id)
        {
#if ENTITY_SYSTEM_DEBUG
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

        public override EntityPrototype GetEntityPrototype(uint prototypeId) => (EntityPrototype)entityPrototypesById[prototypeId].Reference;
        public override bool TryGetEntityPrototype(uint prototypeId, out EntityPrototype entityPrototype)
        {
#if ENTITY_SYSTEM_DEBUG
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

        public override EntityPrototype GetEntityPrototype(string prototypeName) => (EntityPrototype)entityPrototypesByName[prototypeName].Reference;
        public override bool TryGetEntityPrototype(string prototypeName, out EntityPrototype entityPrototype)
        {
#if ENTITY_SYSTEM_DEBUG
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

        public override EntityData GetEntityData(uint entityId) => (EntityData)entityDataById[entityId].Reference;
        public override bool TryGetEntityData(uint entityId, out EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
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

        public override EntityData SendCustomCreateEntityIA(uint iaId, uint prototypeId, Vector3 position, Quaternion rotation)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendCustomCreateEntityIA");
#endif
            if (!lockstep.IsInitialized)
                return null;

            lockstep.WriteSmallUInt(prototypeId);
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            ulong uniqueId = lockstep.SendInputAction(iaId);

            // Latency hiding.
            lockstep.OpenGameStateUnsafeScope(); // The uniqueId is game state unsafe.
            EntityData result = CreateDefaultEntity(
                GetEntityPrototype(prototypeId), uniqueId, InvalidId,
                GetPlayerDataForPlayerId(localPlayerId),
                position, rotation,
                highPriority: true);
            lockstep.CloseGameStateUnsafeScope();
            return result;
        }

        public override EntityData ReadEntityInCustomCreateEntityIA(bool onEntityCreatedGetsRaisedLater = false)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityInCustomCreateEntityIA");
#endif
            uint prototypeId = lockstep.ReadSmallUInt();
            Vector3 position = lockstep.ReadVector3();
            Quaternion rotation = lockstep.ReadQuaternion();
            ulong uniqueId = lockstep.SendingUniqueId;
            uint id = nextEntityId++;

            EntitySystemPlayerData playerData = GetPlayerDataForPlayerId(lockstep.SendingPlayerId);

            EntityData entityData;
            if (lockstep.SendingPlayerId == localPlayerId) // Was latency hidden, promote entityData to game state.
            {
                // In the case of an import having happened between sending and receiving the IA, this is still correct.
                // The import does not delete latency hidden entities which are not yet part of the game state.
                // It is also generally disallowed to cancel the creation of an entity, that would break the
                // guarantee of its lifecycle. (And making developers add support for even more entity
                // lifecycles is unreasonable.)
                // That is also why comparing against the local player id is sufficient for checking for latency
                // hidden IAs, no need for latencyHiddenUniqueIds, because nothing can cancel nor invalidate
                // these latency hidden create entity IAs.
                entityData = (EntityData)entityDataByUniqueId[uniqueId].Reference;
                SetEntityDataId(entityData, id);
            }
            else
            {
                lockstep.OpenGameStateUnsafeScope(); // The condition for this if else block is game state unsafe.
                entityData = CreateDefaultEntity(
                    GetEntityPrototype(prototypeId), uniqueId, id,
                    playerData,
                    position, rotation,
                    highPriority: false);
                lockstep.CloseGameStateUnsafeScope();
            }

            playerData.GainCreated(entityData);
            playerData.GainLastUsed(entityData);

            entityDataLogic.OnEntityDataCreated(entityData);
            if (!onEntityCreatedGetsRaisedLater)
                RaiseOnEntityCreated(entityData);
            return entityData;
        }

        public override void RaiseOnEntityCreatedInCustomCreateEntityIA(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityCreatedInCustomCreateEntityIA");
#endif
            RaiseOnEntityCreated(entityData);
        }

        public override EntityData SendCreateEntityIA(uint prototypeId, Vector3 position, Quaternion rotation)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendCreateEntityIA");
#endif
            return SendCustomCreateEntityIA(createEntityIAId, prototypeId, position, rotation);
        }

        [HideInInspector][SerializeField] private uint createEntityIAId;
        [LockstepInputAction(nameof(createEntityIAId))]
        public void OnCreateEntityIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnCreateEntityIA");
#endif
            ReadEntityInCustomCreateEntityIA();
        }

        private EntityData CreateDefaultEntity(
            EntityPrototype prototype,
            ulong uniqueId,
            uint id,
            EntitySystemPlayerData createdByPlayerData,
            Vector3 position,
            Quaternion rotation,
            bool highPriority)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateDefaultEntity");
#endif
            EntityData entityData = NewEntityData(prototype, uniqueId, id);
            entityDataLogic.InitFromDefault(entityData, position, rotation, prototype.DefaultScale, createdByPlayerData, createdByPlayerData);
            pooling.RequestEntity(entityData, highPriority);
            return entityData;
        }

        public override void EnsureEntityExistsForEntityData(
            EntityData entityData,
            UdonSharpBehaviour callbackInst,
            string callbackEventName,
            object callbackCustomData = null)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  EnsureEntityExistsForEntityData");
#endif
            // Do this check early in here rather than inside of CreateEntityAvoidingRecursion as a micro optimization,
            // reducing the amount of SetProgramVariable calls when the entity already exists.
            // Udon is a great language.
            if (!pooling.IsMissingEntity(entityData))
            {
                RaiseEnsureEntityExistsCallback(entityData, callbackInst, callbackEventName, callbackCustomData);
                return;
            }
            pooling.CreateEntityAvoidingRecursion(entityData, callbackInst, callbackEventName, callbackCustomData);
        }

        private EntityData callbackEntityData;
        public override EntityData CallbackEntityData => callbackEntityData;
        private object callbackCustomData;
        public override object CallbackCustomData => callbackCustomData;

        public void RaiseEnsureEntityExistsCallback(
            EntityData entityData,
            UdonSharpBehaviour callbackInst,
            string callbackEventName,
            object callbackCustomData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseEnsureEntityExistsCallback");
#endif
            callbackEntityData = entityData;
            this.callbackCustomData = callbackCustomData;
            callbackInst.SendCustomEvent(callbackEventName);
        }

        public override void WriteEntityPrototypeRef(EntityPrototype prototype)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityPrototypeRef");
#endif
            lockstep.WriteSmallUInt(prototype.Id);
        }

        public override bool TryReadEntityPrototypeRef(out EntityPrototype prototype)
        {
            return TryReadEntityPrototypeRef(out prototype, lockstep.IsDeserializingForImport);
        }

        public override bool TryReadEntityPrototypeRef(out EntityPrototype prototype, bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryReadEntityPrototypeRef");
#endif
            if (isImport)
            {
                prototype = GetImportedPrototypeMetadata(lockstep.ReadSmallUInt()).entityPrototype;
                return prototype != null;
            }
            prototype = (EntityPrototype)entityPrototypesById[lockstep.ReadSmallUInt()].Reference;
            return true;
        }

        public override EntityPrototype ReadEntityPrototypeRef()
        {
            return ReadEntityPrototypeRef(lockstep.IsDeserializingForImport);
        }

        public override EntityPrototype ReadEntityPrototypeRef(bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityPrototypeRef");
#endif
            return isImport
                ? GetImportedPrototypeMetadata(lockstep.ReadSmallUInt()).entityPrototype
                : (EntityPrototype)entityPrototypesById[lockstep.ReadSmallUInt()].Reference;
        }

        public override void WriteEntityDataRef(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityDataRef");
#endif
            if (entityData == null)
            {
                lockstep.WriteSmallUInt(InvalidId);
                return;
            }
            uint id = entityData.id;
            if (id != InvalidId)
            {
                lockstep.WriteSmallUInt(id);
                return;
            }
            ulong uniqueId = entityData.uniqueId;
            if (uniqueId == InvalidUniqueId)
            {
                Debug.LogError($"[EntitySystem] Attempt to WriteEntityDataRef where both id and uniqueId are invalid.");
                return;
            }
            lockstep.WriteByte(0xff); // WriteSmall never writes 0xff as its first byte.
            lockstep.WriteULong(uniqueId);
        }

        public override bool TryReadEntityDataRef(out EntityData entityData)
        {
            return TryReadEntityDataRef(out entityData, lockstep.IsDeserializingForImport);
        }

        public override bool TryReadEntityDataRef(out EntityData entityData, bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryReadEntityDataRef");
#endif
            if (isImport)
                return TryGetRemappedImportedEntityData(lockstep.ReadSmallUInt(), out entityData);
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
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendTransformChangeIA");
#endif
            return lockstep.SendInputAction(transformChangeIAId);
        }

        [HideInInspector][SerializeField] private uint transformChangeIAId;
        [LockstepInputAction(nameof(transformChangeIAId))]
        public void OnTransformChangeIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnTransformChangeIA");
#endif
            if (!TryReadEntityDataRef(out EntityData entityData, isImport: false))
                return;
            entityDataLogic.OnTransformChangeIA(entityData);
        }

        public override void SendDestroyEntityIA(EntityData entityData, bool keepChildEntitiesAlive = false)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendDestroyEntityIA");
#endif
            if (!entityData.CheckLiveliness())
                return;

            if (!keepChildEntitiesAlive)
            {
                SendDestroyEntityIncludingChildrenIA(entityData);
                return;
            }

            // This check comes here, because when including children, some children may not yet be destroyed.
            if (entityData.entityIsDestroyed)
                return;

            lockstep.WriteSmallUInt(1u); // 1 entity getting destroyed.
            WriteEntityDataRef(entityData);
            lockstep.SendInputAction(destroyEntitiesIAId);
            DestroyEntityInLS(entityData); // Latency hiding.
        }

        private EntityData[] destroyStack = new EntityData[ArrList.MinCapacity];
        private int destroyStackCount = 0;

        private void SendDestroyEntityIncludingChildrenIA(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendDestroyEntityIncludingChildrenIA");
#endif
            // Build a stack where the nodes (entities) deepest into the tree, so the leaves, come last in the stack.
            // This is comparable to a recursive depth first search kind of algorithm, except we cannot use recursion.
            // We want depth first for this, because children of an entity should be destroyed first,
            // which is why the stack ends up getting iterated in reverse (almost like popping each element).
            ArrList.Add(ref destroyStack, ref destroyStackCount, entityData);
            int i = 0;
            int alreadyDestroyedCount = 0;
            do
            {
                EntityData current = destroyStack[i];
                if (current.entityIsDestroyed)
                    alreadyDestroyedCount++; // Check children even of destroyed entities, they might contain non destroyed ones.
                Entity entity = current.entity;
                if (entity != null) // Prefer to use latency state if present.
                    ArrList.AddRange(ref destroyStack, ref destroyStackCount, entity.childEntities, entity.childEntitiesCount);
                else
                    ArrList.AddRange(ref destroyStack, ref destroyStackCount, current.childEntities, current.childEntitiesCount);
            }
            while (++i < destroyStackCount);

            lockstep.WriteSmallUInt((uint)(destroyStackCount - alreadyDestroyedCount));
            while (--i >= 0)
            {
                EntityData current = destroyStack[i];
                if (current.entityIsDestroyed)
                    continue;
                WriteEntityDataRef(current);
                DestroyEntityInLS(current); // Latency hiding.
            }
            ArrList.Clear(ref destroyStack, ref destroyStackCount);

            lockstep.SendInputAction(destroyEntitiesIAId);
        }

        [HideInInspector][SerializeField] private uint destroyEntitiesIAId;
        [LockstepInputAction(nameof(destroyEntitiesIAId))]
        public void OnDestroyEntitiesIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnDestroyEntitiesIA");
#endif
            uint destroyedCount = lockstep.ReadSmallUInt();
            for (uint i = 0; i < destroyedCount; i++)
                if (TryReadEntityDataRef(out EntityData entityData, isImport: false))
                    DestroyEntityInGS(entityData);
        }

        public override void SendDestroyAllEntitiesIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendDestroyAllEntitiesIA");
#endif
            lockstep.SendInputAction(destroyAllEntitiesIAId);
        }

        [HideInInspector][SerializeField] private uint destroyAllEntitiesIAId;
        [LockstepInputAction(nameof(destroyAllEntitiesIAId))]
        public void OnDestroyAllEntitiesIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnDestroyAllEntitiesIA");
#endif
            DestroyAllEntities();
        }

        private void DestroyAllEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyAllEntities");
#endif
            for (int i = allEntityDataCount - 1; i >= 0; i--)
                DestroyEntityInGS(allEntityData[i]);
        }

        private int destroyNonPreInstantiatedEntitiesIndex = -1;
        private void DestroyNonPreInstantiatedEntities()
        {
#if ENTITY_SYSTEM_DEBUG
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
                DestroyEntityInGS(entityData);
                if (isImporting && WorkIsRunningLong())
                {
                    destroyNonPreInstantiatedEntitiesIndex = i;
                    return;
                }
            }
            destroyNonPreInstantiatedEntitiesIndex = -1;
            if (isImporting)
                deSerializationStage++;
        }

        private int destroyEntitiesWhichWereNotImportedIndex = -1;
        private void DestroyEntitiesWhichWereNotImported()
        {
#if ENTITY_SYSTEM_DEBUG
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
                DestroyEntityInGS(entityData);
                if (WorkIsRunningLong())
                {
                    destroyEntitiesWhichWereNotImportedIndex = i;
                    return;
                }
            }
            destroyEntitiesWhichWereNotImportedIndex = -1;
            deSerializationStage++;
        }

        public override void DestroyEntityInGS(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyEntityInGS");
#endif
            if (!entityData.CheckLiveliness() || entityData.instanceIndex == -1)
                return;
            DestroyEntityInLS(entityData);
            if (entityData.uniqueId != InvalidUniqueId)
                entityDataByUniqueId.Remove(entityData.uniqueId);
            DeregisterEntityDataAndId(entityData);
            entityDataLogic.OnEntityDataDestroyed(entityData);
            RaiseOnEntityDestroyed(entityData);
            entityData.DecrementRefsCount();
        }

        private void DestroyEntityInLS(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyEntityInLS");
#endif
            pooling.ReturnEntity(entityData);
        }

        public override void WriteEntityExtensionDataRef(EntityExtensionData extensionData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityExtensionDataRef");
#endif
            WriteEntityDataRef(extensionData.entityData);
            lockstep.WriteSmallUInt((uint)extensionData.extensionIndex);
        }

        public override EntityExtensionData ReadEntityExtensionDataRefDynamic()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityExtensionDataRefDynamic");
#endif
            bool foundEntityData = TryReadEntityDataRef(out EntityData entityData);
            // TODO: Can and should this have import support, I wonder?
            // Must read the extensionIndex even if the entityData was not found otherwise the read stream position ends up being wrong.
            int extensionIndex = (int)lockstep.ReadSmallUInt();
            return foundEntityData
                ? entityData.allExtensionData[extensionIndex]
                : null;
        }

        private byte[] sendExtensionDataInputActionBuffer = new byte[5 * 3]; // Max size of 3 SmallUInt.
        public override ulong SendExtensionDataInputAction(EntityExtensionData extensionData, string methodName)
        {
#if ENTITY_SYSTEM_DEBUG
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
#if ENTITY_SYSTEM_DEBUG
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

        #region Serialization

        private void PrepareForBuildSortedEntityDataList()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  PrepareForBuildSortedEntityDataList");
#endif
            allEntityDataSorted = new EntityData[allEntityDataCount];
            deSerializationStage++;
        }

        private void BuildSortedEntityDataList()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  BuildSortedEntityDataList");
#endif
            while (suspendedAllEntityDataSortedCount < allEntityDataCount)
            {
                if (WorkIsRunningLong())
                    return;

                // This logic quite simply orders the serialization of entities such that roots (in terms of parenting)
                // come first, followed by their children, then the children of children, so that whole tree.
                // Then moving on to the next root.
                // Deserialization requires the guarantee that parents come before their children.
                EntityData entityData;
                if (suspendedIndexInAllEntityDataSorted < suspendedAllEntityDataSortedCount)
                    entityData = allEntityDataSorted[suspendedIndexInAllEntityDataSorted++];
                else
                {
                    entityData = allEntityData[suspendedIndexInArray++];
                    if (entityData.parentEntityData != null)
                        continue;
                    allEntityDataSorted[suspendedAllEntityDataSortedCount++] = entityData;
                    suspendedIndexInAllEntityDataSorted++;
                }

                ArrList.AddRange(ref allEntityDataSorted, ref suspendedAllEntityDataSortedCount, entityData.childEntities, entityData.childEntitiesCount);
            }

            if (suspendedAllEntityDataSortedCount != allEntityDataCount)
                Debug.LogError($"[EntitySystem] The game state contains broken parent child relationships. "
                    + $"When finding all root entities and visiting them and their children recursively, "
                    + $"the total amount of entities visited ({suspendedAllEntityDataSortedCount}) does "
                    + $"not match the total entity count ({allEntityDataCount}). This must be impossible.");

            suspendedAllEntityDataSortedCount = 0;
            suspendedIndexInAllEntityDataSorted = 0;
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void CleanupSortedEntityDataList()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  CleanupSortedEntityDataList");
#endif
            allEntityDataSorted = null;
            deSerializationStage++;
        }

        private void Export()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  Export");
#endif
            if (deSerializationStage == 0)
            {
                lockstep.WriteSmallUInt(highestPreInstantiatedEntityId);
                deSerializationStage++;
            }
            if (deSerializationStage == 1)
                WriteAllExportedPrototypeMetadata();
            if (deSerializationStage == 2)
            {
                if (!optionsGS.ExportOptions.includeEntities)
                {
                    deSerializationStage = 0;
                    return;
                }
                deSerializationStage++;
            }
            if (deSerializationStage == 3)
                PrepareForBuildSortedEntityDataList();
            if (deSerializationStage == 4)
                BuildSortedEntityDataList();
            if (deSerializationStage == 5)
                WriteExportedIdsCount();
            if (deSerializationStage == 6)
                WriteExportedIds();
            if (deSerializationStage == 7)
                WriteExportedEntities();
            if (deSerializationStage == 8)
                CleanupSortedEntityDataList();
            if (deSerializationStage == 9)
                deSerializationStage = 0;
        }

        private string Import(uint importedDataVersion)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  Import");
#endif
            if (deSerializationStage == 0)
            {
                importedPrototypeMetadataById = new DataDictionary();
                remappedImportedEntityData = new DataDictionary();
                highestImportedPreInstantiatedEntityId = lockstep.ReadSmallUInt();
                deSerializationStage++;
            }
            if (deSerializationStage == 1)
                ReadAllImportedPrototypeMetadata();
            if (deSerializationStage == 2)
            {
                if (!optionsGS.OptionsFromExport.includeEntities || !optionsGS.ImportOptions.includeEntities)
                {
                    deSerializationStage = 0;
                    return null;
                }
                deSerializationStage++;
            }
            // There's technically no reason for this to happen separately to
            // DestroyEntitiesWhichWereNotImported, but this is easier to read.
            if (deSerializationStage == 3)
                DestroyNonPreInstantiatedEntities();
            if (deSerializationStage == 4)
                ReadImportedIdsCount();
            if (deSerializationStage == 5)
                ReadImportedIds();
            if (deSerializationStage == 6)
                DestroyEntitiesWhichWereNotImported();
            if (deSerializationStage == 7)
                PrepareForReadAndCreateImportedEntities();
            if (deSerializationStage == 8)
                ReadAndCreateImportedEntities(importedDataVersion);
            if (deSerializationStage == 9)
                ClearEntityPrototypeMetadataReferences();
            if (deSerializationStage == 10)
            {
                deSerializationStage = 0;
                allImportedEntityData = null;
            }
            return null;
        }

        [LockstepEvent(LockstepEventType.OnImportFinished, Order = 10000)]
        public void OnImportFinished()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnImportFinished");
#endif
            if (!IsPartOfCurrentImport)
                return;
            // Kept these alive for longer so game states importing after the EntitySystem could still use them.
            DataList list = importedPrototypeMetadataById.GetValues();
            for (int i = 0; i < list.Count; i++)
                ((EntityPrototypeMetadata)list[i].Reference).DecrementRefsCount();
            importedPrototypeMetadataById = null;
            remappedImportedEntityData = null;
        }

        public override EntityPrototypeMetadata GetImportedPrototypeMetadata(uint prototypeId) => (EntityPrototypeMetadata)importedPrototypeMetadataById[prototypeId].Reference;
        public override bool TryGetImportedPrototypeMetadata(uint prototypeId, out EntityPrototypeMetadata metadata)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetImportedPrototypeMetadata");
#endif
            if (importedPrototypeMetadataById.TryGetValue(prototypeId, out DataToken token))
            {
                metadata = (EntityPrototypeMetadata)token.Reference;
                return true;
            }
            metadata = null;
            return false;
        }

        public override EntityData GetRemappedImportedEntityData(uint importedId) => (EntityData)remappedImportedEntityData[importedId].Reference;
        public override bool TryGetRemappedImportedEntityData(uint importedId, out EntityData remappedEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
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

        private void WriteAllExportedPrototypeMetadata()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteAllExportedPrototypeMetadata");
#endif
            // Write metadata regardless of if entities get exported as other systems may depend on that
            // metadata in imports. This is invisible to the user anyway, aside from inflating export size and
            // taking import time due to udon behavior instantiation.
            lockstep.WriteSmallUInt((uint)entityPrototypes.Length);
            foreach (EntityPrototype prototype in entityPrototypes)
                prototype.ExportMetadata();
            deSerializationStage++;
        }

        private int prototypeMetadataToImportCount = -1;
        private void ReadAllImportedPrototypeMetadata()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadAllImportedPrototypeMetadata");
#endif
            if (prototypeMetadataToImportCount == -1)
                prototypeMetadataToImportCount = (int)lockstep.ReadSmallUInt();
            while (suspendedIndexInArray < prototypeMetadataToImportCount)
            {
                EntityPrototypeMetadata metadata = EntityPrototypeStatics.ImportMetadata(wannaBeClasses, lockstep, this);
                importedPrototypeMetadataById.Add(metadata.id, metadata);
                suspendedIndexInArray++;
                if (WorkIsRunningLong())
                    return;
            }
            prototypeMetadataToImportCount = -1;
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void WriteExportedIdsCount()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteExportedIdsCount - allEntityDataCount: {allEntityDataCount}");
#endif
            lockstep.WriteSmallUInt((uint)allEntityDataCount);
            deSerializationStage++;
        }

        private void ReadImportedIdsCount()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadImportedIdsCount");
#endif
            allImportedEntityData = new EntityData[(int)lockstep.ReadSmallUInt()];
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadImportedIdsCount (inner) - allImportedEntityData.Length: {allImportedEntityData.Length}");
#endif
            deSerializationStage++;
        }

        private void WriteExportedIds()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteExportedIds");
#endif
            while (suspendedIndexInArray < allEntityDataCount)
            {
                // Unlike LJ GS serialization, exports can use the sorted list, because
                // in exports the order of entities is not required to match the current state.
                // And by WriteExportedIds and WriteExportedEntities both using the same order,
                // WriteExportedEntities does not need to serialize each entity's id again,
                // unlike SerializeEntities.
                EntityData entityData = allEntityDataSorted[suspendedIndexInArray++];
                lockstep.WriteSmallUInt(entityData.id);
                lockstep.WriteSmallUInt(entityData.entityPrototype.Id);
                if (WorkIsRunningLong())
                    return;
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void ReadImportedIds()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadImportedIds");
#endif
            int length = allImportedEntityData.Length;
            while (suspendedIndexInArray < length)
            {
                uint id = lockstep.ReadSmallUInt();
                uint prototypeId = lockstep.ReadSmallUInt();
                EntityPrototypeMetadata metadata = GetImportedPrototypeMetadata(prototypeId);
                if (metadata.entityPrototype == null)
                {
                    suspendedIndexInArray++;
                    continue;
                }
                EntityData entityData = (IsPreInstantiatedEntityId(id, isImport: true)
                    && TryGetEntityData(id, out EntityData existingEntityData)
                    && existingEntityData.entityPrototype == metadata.entityPrototype)
                    ? existingEntityData
                    : NewEntityData(metadata.entityPrototype, InvalidUniqueId, nextEntityId++);
                entityData.importedMetadata = metadata;
                allImportedEntityData[suspendedIndexInArray++] = entityData;
                remappedImportedEntityData.Add(id, entityData);
                if (WorkIsRunningLong())
                    return;
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void WriteExportedEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteExportedEntities");
#endif
            while (suspendedIndexInArray < allEntityDataCount)
            {
                entityDataLogic.Serialize(allEntityDataSorted[suspendedIndexInArray++], isExport: true);
                if (WorkIsRunningLong())
                    return;
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void PrepareForReadAndCreateImportedEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  PrepareForReadAndCreateImportedEntities");
#endif
            dummyEntityDataForImport = NewEntityData(prototype: null, InvalidUniqueId, InvalidId);
            deSerializationStage++;
        }

        private void ReadAndCreateImportedEntities(uint importedDataVersion)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadAndCreateImportedEntities");
#endif
            int length = allImportedEntityData.Length;
            while (suspendedIndexInArray < length)
            {
                if (WorkIsRunningLong())
                    return;
                EntityData entityData = allImportedEntityData[suspendedIndexInArray++];
                if (entityData == null)
                {
                    entityDataLogic.Deserialize(dummyEntityDataForImport, isImport: true, importedDataVersion);
                    continue;
                }
                entityDataLogic.Deserialize(entityData, isImport: true, importedDataVersion);
                if (entityData.entity != null)
                    entityData.entity.ApplyEntityData();
                else
                    pooling.RequestEntity(entityData);
            }
            dummyEntityDataForImport.Delete();
            dummyEntityDataForImport = null;
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void ClearEntityPrototypeMetadataReferences()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  ClearEntityPrototypeMetadataReferences");
#endif
            // Clear references to what is going to be empty unity object reference objects so those can get GCed too.
            foreach (EntityData entityData in allImportedEntityData)
                if (entityData != null)
                    entityData.importedMetadata = null;
            deSerializationStage++;
        }

        private void SerializeEntityDataId(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeEntityDataId - entityData.id: {entityData.id}, entityData.entityPrototype.Id: {entityData.entityPrototype.Id}");
#endif
            lockstep.WriteSmallUInt(entityData.id);
            if (!IsPreInstantiatedEntityId(entityData.id, isImport: false))
                lockstep.WriteULong(entityData.uniqueId);
            lockstep.WriteSmallUInt(entityData.entityPrototype.Id);
        }

        private void DeserializeEntityDataId()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeEntityDataId");
#endif
            uint id = lockstep.ReadSmallUInt();
            bool isPreInstantiated = IsPreInstantiatedEntityId(id, isImport: false);
            ulong uniqueId = isPreInstantiated ? InvalidUniqueId : lockstep.ReadULong();
            uint prototypeId = lockstep.ReadSmallUInt();
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeEntityData (inner) - id: {id}, uniqueId: 0x{uniqueId:x16}, prototypeId: {prototypeId}");
#endif
            if (isPreInstantiated)
                DeserializePreInstantiatedEntityId(id, prototypeId);
            else
                NewEntityData(GetEntityPrototype(prototypeId), uniqueId, id);
        }

        private void DeserializePreInstantiatedEntityId(uint id, uint prototypeId)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializePreInstantiatedEntityId");
#endif
            if (!preInstantiatedEntityIndexById.TryGetValue(id, out DataToken indexToken))
            {
                Debug.LogError($"[EntitySystem] Impossible, the incoming/deserialized id {id} should be an id of "
                    + $"a pre instantiated entity, however there is no pre instantiated entity with this id.");
                // Create a new entity to recover.
                NewEntityData(GetEntityPrototype(prototypeId), InvalidUniqueId, id);
                return;
            }
            int index = indexToken.Int;
            preInstantiatedEntityInstanceIds[index] = InvalidId; // Mark as used.

            EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[index];
            EntityData entityData = preInstantiatedEntityData[index];
            entityDataLogic.WannaBeConstructor(entityData, prototype, InvalidUniqueId, id);
            RegisterEntityDataAndId(entityData);
        }

        public override void SerializeGameState(bool isExport, LockstepGameStateOptionsData exportOptions)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeGameState - deSerializationStage: {deSerializationStage}");
#endif
            workSw.Restart();

            if (isExport)
            {
                Export();
                return;
            }

            if (deSerializationStage == 0)
            {
                lockstep.WriteSmallUInt(nextEntityId);
                deSerializationStage++;
            }
            if (deSerializationStage == 1)
                PrepareForBuildSortedEntityDataList();
            if (deSerializationStage == 2)
                BuildSortedEntityDataList();
            if (deSerializationStage == 3)
                SerializeEntityIdsCount();
            if (deSerializationStage == 4)
                SerializeEntityIds();
            if (deSerializationStage == 5)
                SerializeEntities();
            if (deSerializationStage == 6)
                CleanupSortedEntityDataList();
            if (deSerializationStage == 7)
                deSerializationStage = 0;
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion, LockstepGameStateOptionsData importOptions)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeGameState - deSerializationStage: {deSerializationStage}");
#endif
            workSw.Restart();

            if (isImport)
                return Import(importedDataVersion);

            if (deSerializationStage == 0)
            {
                nextEntityId = lockstep.ReadSmallUInt();
                deSerializationStage++;
            }
            if (deSerializationStage == 1)
                PrepareForPreInstantiatedEntityDeserialization();
            if (deSerializationStage == 2)
                DeserializeEntityIdsCount();
            if (deSerializationStage == 3)
                DeserializeEntityIds();
            if (deSerializationStage == 4)
                DestroyUnusedPreInstantiatedEntities();
            if (deSerializationStage == 5)
                DeserializeEntities();
            if (deSerializationStage == 6)
                DeserializationOfPreInstantiatedEntitiesFinished();
            if (deSerializationStage == 7)
                deSerializationStage = 0;
            return null;
        }

        private void PrepareForPreInstantiatedEntityDeserialization()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  PrepareForPreInstantiatedEntityDeserialization");
#endif
            // Required by entity id deserialization.
            preInstantiatedEntityIndexById = new DataDictionary();
            for (int i = 0; i < preInstantiatedEntityInstanceIds.Length; i++)
                preInstantiatedEntityIndexById.Add(preInstantiatedEntityInstanceIds[i], i);
            deSerializationStage++;
        }

        private void SerializeEntityIdsCount()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeEntityIdsCount");
#endif
            lockstep.WriteSmallUInt((uint)allEntityDataCount);
            deSerializationStage++;
        }

        private void DeserializeEntityIdsCount()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeEntityIdsCount");
#endif
            entitiesToDeserializeCount = (int)lockstep.ReadSmallUInt();
            ArrList.EnsureCapacity(ref allEntityData, entitiesToDeserializeCount);
            deSerializationStage++;
        }

        private void SerializeEntityIds()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeEntityIds");
#endif
            while (suspendedIndexInArray < allEntityDataCount)
            {
                // Must use allEntityData, not allEntityDataSorted, because the order of allEntityData
                // is part of the game state and must be restored as such on the joining client.
                SerializeEntityDataId(allEntityData[suspendedIndexInArray++]);
                if (WorkIsRunningLong())
                    return;
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void DeserializeEntityIds()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeEntityIds");
#endif
            while (suspendedIndexInArray < entitiesToDeserializeCount)
            {
                DeserializeEntityDataId();
                suspendedIndexInArray++;
                if (WorkIsRunningLong())
                    return;
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void DestroyUnusedPreInstantiatedEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DestroyUnusedPreInstantiatedEntities");
#endif
            int length = preInstantiatedEntityInstanceIds.Length;
            while (suspendedIndexInArray < length)
            {
                if (preInstantiatedEntityInstanceIds[suspendedIndexInArray] == InvalidId)
                {
                    suspendedIndexInArray++;
                    continue;
                }
                // This makes both entities and entityData go through their usual lifecycle, which ultimately
                // allows for the entity instances to be put into the entity pool rather than just being
                // destroyed right now.
                EntityData entityData = InitPreInstantiatedEntity(suspendedIndexInArray++);
                DestroyEntityInGS(entityData);
                if (WorkIsRunningLong())
                    return;
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void SerializeEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeEntities");
#endif
            while (suspendedIndexInArray < allEntityDataCount)
            {
                if (WorkIsRunningLong())
                    return;
                EntityData entityData = allEntityDataSorted[suspendedIndexInArray++];
                // Entity data ids and entity data gets serialized in a different order. Must write
                // the associated id for deserialization to know which entity data to read into.
                lockstep.WriteSmallUInt(entityData.id);
                entityDataLogic.Serialize(entityData, isExport: false);
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void DeserializeEntities()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeEntities");
#endif
            while (suspendedIndexInArray < entitiesToDeserializeCount)
            {
                uint id = lockstep.ReadSmallUInt();
                EntityData entityData = (EntityData)entityDataById[id].Reference;
                entityDataLogic.Deserialize(entityData, isImport: false, importedDataVersion: 0u);
                RequestOrAssociateDeserializedEntity(entityData);
                RaiseOnEntityDeserialized(entityData);
                suspendedIndexInArray++;
                if (WorkIsRunningLong())
                    return;
            }
            suspendedIndexInArray = 0;
            deSerializationStage++;
        }

        private void RequestOrAssociateDeserializedEntity(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RequestOrAssociateDeserializedEntity");
#endif
            uint id = entityData.id;
            if (!IsPreInstantiatedEntityId(id, isImport: false)
                || !preInstantiatedEntityIndexById.TryGetValue(id, out DataToken indexToken))
            {
                pooling.RequestEntity(entityData);
                return;
            }

            Entity entity = preInstantiatedEntityInstances[indexToken.Int];
            if (entity != null)
            {
                entityLogic.AssociateWithEntityData(entity, entityData);
                return;
            }

            Debug.LogError($"[EntitySystem] A pre instantiated entity was deleted before initialization "
                + $"in a non deterministic fashion. Prototype name: {entityData.entityPrototype.PrototypeName}");
            pooling.RequestEntity(entityData); // To recover, instantiate a new entity.
        }

        private void DeserializationOfPreInstantiatedEntitiesFinished()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializationOfPreInstantiatedEntitiesFinished");
#endif
            preInstantiatedEntityInstances = null;
            preInstantiatedEntityInstanceIds = null;
            preInstantiatedEntityInstancePrototypes = null;
            preInstantiatedEntityData = null;
            preInstantiatedEntityIndexById = null;
            deSerializationStage++;
        }

        #endregion

        #region EventDispatcher

        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onEntityDeserializedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onEntityCreatedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onEntityDestroyedListeners;

        private EntityData deserializedEntityData;
        public override EntityData DeserializedEntityData => deserializedEntityData;
        private void RaiseOnEntityDeserialized(EntityData deserializedEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityDeserialized");
#endif
            this.deserializedEntityData = deserializedEntityData;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onEntityDeserializedListeners, nameof(EntitySystemEventType.OnEntityDeserialized));
            this.deserializedEntityData = null; // To prevent misuse of the API.
        }

        private EntityData createdEntityData;
        public override EntityData CreatedEntityData => createdEntityData;
        private void RaiseOnEntityCreated(EntityData createdEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityCreated");
#endif
            this.createdEntityData = createdEntityData;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onEntityCreatedListeners, nameof(EntitySystemEventType.OnEntityCreated));
            this.createdEntityData = null; // To prevent misuse of the API.
        }

        private EntityData destroyedEntityData;
        public override EntityData DestroyedEntityData => destroyedEntityData;
        private void RaiseOnEntityDestroyed(EntityData destroyedEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityDestroyed");
#endif
            this.destroyedEntityData = destroyedEntityData;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onEntityDestroyedListeners, nameof(EntitySystemEventType.OnEntityDestroyed));
            this.destroyedEntityData = null; // To prevent misuse of the API.
        }

        #endregion
    }
}
