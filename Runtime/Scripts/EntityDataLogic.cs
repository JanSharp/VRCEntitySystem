using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    public class EntityDataLogic : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private LockstepAPI lockstep;
        [HideInInspector][SerializeField][SingletonReference] private EntitySystemAPI entitySystem;
        [HideInInspector][SerializeField][SingletonReference] private InterpolationManager interpolation;
        [HideInInspector][SerializeField][SingletonReference] private WannaBeClassesManager wannaBeClasses;
        [HideInInspector][SerializeField][SingletonReference] private EntityLogic entityLogic;

        private bool IsDummyEntityDataForImport(EntityData entityData) => entityData.entityPrototype == null;

        public void WannaBeConstructor(EntityData entityData, EntityPrototype entityPrototype, ulong uniqueId, uint id)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  WannaBeConstructor - entityPrototype.PrototypeName: {(entityPrototype == null ? "<null>" : entityPrototype.PrototypeName)}, uniqueId: 0x{uniqueId:x16}, id: {id}");
#endif
            entityData.entityPrototype = entityPrototype;
            entityData.uniqueId = uniqueId;
            entityData.id = id;

            if (IsDummyEntityDataForImport(entityData))
                return;

            string[] extensionClassNames = entityPrototype.ExtensionDataClassNames;
            int length = extensionClassNames.Length;
            if (entityData.allExtensionData.Length == length) // Already populated for pre instantiated EntityData.
                for (int i = 0; i < length; i++)
                    entityData.allExtensionData[i].WannaBeConstructor(i, entityData);
            else
            {
                entityData.allExtensionData = new EntityExtensionData[length];
                for (int i = 0; i < length; i++)
                    entityData.allExtensionData[i] = EntityExtensionDataStatics.New(wannaBeClasses, extensionClassNames[i], i, entityData);
            }
        }

        public void WannaBeDestructor(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  WannaBeDestructor");
#endif
            if (entityData.allExtensionData == null)
                return;
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                extensionData.DecrementRefsCount();
        }

        public void SetEntity(EntityData entityData, Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  SetEntity");
#endif
            entityData.entity = entity;
            entity.entityData = entityData;
        }

        public void InitFromDefault(
            EntityData entityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            EntitySystemPlayerData createdByPlayerData,
            EntitySystemPlayerData lastUserPlayerData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  InitFromDefault");
            if (entityData.isInitialized)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromDefault "
                    + $"EntityData which has already been initialized.");
#endif
            entityData.isInitialized = true;
            entityData.position = position;
            entityData.rotation = rotation;
            entityData.scale = scale;
            // Intentionally bypassing the setter because this event is not game state safe.
            // The GainCreated and GainLastUsed methods will be called later in the entity creation input action.
            entityData.createdByPlayerData = createdByPlayerData;
            entityData.lastUserPlayerData = lastUserPlayerData;
            entityData.hidden = false;
            entityData.parentEntity = null;

            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            int length = allExtensionData.Length;
            EntityExtension[] defaultExtensions = entityData.entityPrototype.DefaultEntityInst.extensions;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromDefault(defaultExtensions[i]);
        }

        public void InitFromPreInstantiated(EntityData entityData, Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  InitFromPreInstantiated");
            if (entityData.isInitialized)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromPreInstantiated "
                    + $"EntityData which has already been initialized.");
#endif
            entityData.isInitialized = true;
            Transform t = entity.transform;
            entityData.position = t.position;
            entityData.rotation = t.rotation;
            entityData.scale = t.localScale;
            // These 2 are null anyway, but just for clarity.
            // And it is semantically correct to use the property setter here, this is a game state safe event.
            entityData.CreatedByPlayerData = null;
            entityData.LastUserPlayerData = null;
            entityData.hidden = false;
            entityData.parentEntity = null;

            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            EntityExtension[] extensions = entity.extensions;
            int length = allExtensionData.Length;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromPreInstantiated(extensions[i]);
        }

        private void InitAllExtensionDataBeforeDeserialization(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  InitAllExtensionDataBeforeDeserialization");
            if (entityData.isInitialized)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitAllExtensionDataBeforeDeserialization "
                    + $"EntityData which has already been initialized.");
#endif
            entityData.isInitialized = true;
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                extensionData.InitBeforeDeserialization();
        }

        public void OnEntityDataCreated(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  OnEntityDataCreated");
#endif
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                extensionData.OnEntityExtensionDataCreated();
        }

        public void OnAssociatedWithEntity(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  OnAssociatedWithEntity");
#endif
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                extensionData.OnAssociatedWithExtension();
        }

        public void OnDisassociateFromEntity(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  OnDisassociateFromEntity");
#endif
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                extensionData.OnDisassociateFromExtension();
        }

        public void OnEntityDataDestroyed(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  OnEntityDataDestroyed");
#endif
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                extensionData.OnEntityExtensionDataDestroyed();
        }

        /// <summary>
        /// <para>Make all changes to the game state before calling this function, modify the latency state
        /// after, but only if this returned <see langword="true"/>.</para>
        /// </summary>
        /// <returns></returns>
        public bool ShouldApplyReceivedIAToLatencyState(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ShouldApplyReceivedIAToLatencyState - uniqueId: 0x{lockstep.SendingUniqueId:x16}, entityData.latencyHiddenUniqueIds.Count: {entityData.latencyHiddenUniqueIds.Count}");
#endif
            DataDictionary latencyHiddenUniqueIds = entityData.latencyHiddenUniqueIds;
            if (latencyHiddenUniqueIds.Count == 0)
                return true;
            if (latencyHiddenUniqueIds.Remove(lockstep.SendingUniqueId)) // Was already applied to latency state, stay in latency state.
                return false;
            // The latency state is desynced from the game state, however an input action which has not been
            // applied to the game state has been received in between input actions which have already been
            // applied to the latency state. Cannot just apply this IA to the latency state, because that way
            // IAs got applied to the latency state in different order than the game state, so reset the
            // latency state to match the game state entirely instead. This undoes some IAs which had already
            // been applied to the latency state and they are going to get applied again whenever the
            // associated IA gets run in the game state.
            latencyHiddenUniqueIds.Clear();
            Entity entity = entityData.entity;
            if (entity != null)
                entityLogic.ApplyEntityData(entity);
            return false; // Already got applied by the above.
        }

        /// <summary>
        /// <para>Do not modify the game state nor latency state after calling this function.</para>
        /// </summary>
        public void MarkLatencyHiddenUniqueIdAsProcessed(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  MarkLatencyHiddenUniqueIdAsProcessed");
#endif
            ShouldApplyReceivedIAToLatencyState(entityData); // Literally the same logic.
        }

        /// <summary>
        /// <para>If the latency state differs from the game state, reset it to the game state. Any IAs which
        /// had already been applied to the latency state will get applied again whenever the associated IA
        /// runs in the game state.</para>
        /// <para>This enables modification of entities through non latency hidden yet game state safe
        /// events.</para>
        /// <para>It is recommended to modify the game state of entities before calling
        /// <see cref="ResetLatencyStateIfItDiverged"/>. Then if this returns <see langword="false"/> modify
        /// the associated latency state. When this returns <see langword="true"/> then the changes made to
        /// the game state will already have been applied to the latency state through
        /// <see cref="EntityExtension.ApplyExtensionData"/>.</para>
        /// </summary>
        /// <returns><see langword="true"/> if the latency state had indeed diverged, and it has been
        /// reset.</returns>
        public bool ResetLatencyStateIfItDiverged(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ResetLatencyStateIfItDiverged");
#endif
            DataDictionary latencyHiddenUniqueIds = entityData.latencyHiddenUniqueIds;
            if (latencyHiddenUniqueIds.Count == 0)
                return false;
            latencyHiddenUniqueIds.Clear();
            Entity entity = entityData.entity;
            if (entity != null)
                entityLogic.ApplyEntityData(entity);
            return true;
        }

        /// <summary>
        /// <para>Resets the latency state to the game state, but only if the currently running input action
        /// was latency hidden (aka sent by) the local player.</para>
        /// <para>Useful for cases where an input action was applied to the latency state at the time of
        /// sending, so latency hidden, however when actually running that input action in the game state it
        /// turns out that what was applied to the latency state was actually incorrect and the input action
        /// had to do something different.</para>
        /// </summary>
        /// <returns><see langword="true"/> if the input action had been latency hidden by the local player,
        /// and the latency state has been reset.</returns>
        public bool ResetLatencyStateBecauseIAGotAppliedDifferently(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ResetLatencyStateBecauseIAGotAppliedDifferently");
#endif
            DataDictionary latencyHiddenUniqueIds = entityData.latencyHiddenUniqueIds;
            if (!latencyHiddenUniqueIds.ContainsKey(lockstep.SendingUniqueId))
                return false;
            latencyHiddenUniqueIds.Clear();
            Entity entity = entityData.entity;
            if (entity != null)
                entityLogic.ApplyEntityData(entity);
            return true;
        }

        /// <summary>
        /// <para>Use this in cases where it is required for entity data to have up to date game state safe
        /// transform values for a given input action.</para>
        /// <para>This is required due to the potential existence of transform sync controllers which could
        /// make the current transform values of an entity unknown to the game state.</para>
        /// </summary>
        public void WritePotentiallyUnknownTransformValues(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityDataLogic  WritePotentiallyUnknownTransformValues");
#endif
            Entity entity = entityData.entity;
            if (!entityData.noTransformSync // At the time of sending the data was known, so we will have close to up to date values for sure.
                || entity == null // May or may not be unknown by the current transform controller, but we couldn't send any transform data anyway without an entity transform.
                || !entity.noTransformSync // Control has already been given back in the latency state, we are going to have up to date values.
                || lockstep.CurrentTick - entityData.lastKnownTransformStateTick <= LockstepAPI.TickRateUInt) // We knew transform values 1 second ago, that'll be good enough.
            {
                lockstep.WriteByte(0);
                return;
            }

            // Use latency state controller as it will most likely be the controller at the time of receiving data.
            EntityTransformController controller = entity.transformSyncController;
            bool unknownPosition = !controller.TryGetGameStatePosition(entityData, out var discard1); // Cannot use 'out _'.
            bool unknownRotation = !controller.TryGetGameStateRotation(entityData, out var discard2); // Cannot use 'out _'.
            bool unknownScale = !controller.TryGetGameStateScale(entityData, out var discard3); // Cannot use 'out _'.

            lockstep.WriteFlags(unknownPosition, unknownRotation, unknownScale);
            Transform entityTransform = entity.transform;
            if (unknownPosition)
                lockstep.WriteVector3(entityTransform.position);
            if (unknownRotation)
                lockstep.WriteQuaternion(entityTransform.rotation);
            if (unknownScale)
                lockstep.WriteVector3(entityTransform.localScale);
        }

        public void ReadPotentiallyUnknownTransformValues(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityDataLogic  ReadPotentiallyUnknownTransformValues");
#endif
            lockstep.ReadFlags(out bool unknownPosition, out bool unknownRotation, out bool unknownScale);
            if (unknownPosition)
                entityData.position = lockstep.ReadVector3();
            if (unknownRotation)
                entityData.rotation = lockstep.ReadQuaternion();
            if (unknownScale)
                entityData.scale = lockstep.ReadVector3();
        }

        public void TakeControlOfTransformSync(EntityData entityData, EntityTransformController controller)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityDataLogic  TakeControlOfTransformSync");
#endif
            if (!entityData.noTransformSync)
            {
                entityData.noTransformSync = true;
                entityData.lastKnownTransformStateTick = lockstep.CurrentTick;
                entityData.transformSyncController = controller;
                return;
            }
            EntityTransformController prevController = entityData.transformSyncController;
            if (controller != prevController)
            {
                entityData.transformSyncController = controller;
                prevController.OnControlTakenOver(entityData, controller);
            }
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfTransformSync(
            EntityData entityData,
            EntityTransformController releasingController,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityDataLogic  GiveBackControlOfTransformSync");
#endif
            if (!entityData.noTransformSync || (releasingController != null && entityData.transformSyncController != releasingController))
                return;
            entityData.noTransformSync = false;
            entityData.lastKnownTransformStateTick = 0u;
            entityData.position = position;
            entityData.rotation = rotation;
            entityData.scale = scale;
            EntityTransformController prevController = entityData.transformSyncController;
            entityData.transformSyncController = null;
            if (releasingController == null)
                prevController.OnControlLost(entityData);
        }

        public void SetTransformSyncControllerDueToDeserialization(EntityData entityData, EntityTransformController controller)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityDataLogic  SetTransformSyncControllerDueToDeserialization");
#endif
            if (!entityData.noTransformSync)
            {
                Debug.LogError($"[EntitySystem] Attempt to SetTransformSyncControllerDueToDeserialization when "
                    + $"noTransformSync is false, aka should not be any transform sync controller.");
                return;
            }
            if (entityData.transformSyncController != null)
            {
                Debug.LogError($"[EntitySystem] Attempt to SetTransformSyncControllerDueToDeserialization when "
                    + $"a different system has already set a controller. One of these systems is misbehaving.");
                return;
            }
            entityData.transformSyncController = controller;
        }

        private void SerializeTransformValues(EntityData entityData, bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  SerializeTransformValues");
#endif
            Entity entity = entityData.entity;
            bool noTransformSync = entityData.noTransformSync;
            if (isExport && noTransformSync && entity != null)
            {
                // Exports can actually use non game state safe data.
                // Imports are going to turn that data into game state safe data anyway.
                Transform entityTransform = entity.transform;
                lockstep.WriteVector3(entityTransform.position);
                lockstep.WriteQuaternion(entityTransform.rotation);
                lockstep.WriteVector3(entityTransform.localScale);
                lockstep.WriteSmallUInt(0u); // ticksAgo
                return;
            }

            lockstep.WriteVector3(entityData.position);
            lockstep.WriteQuaternion(entityData.rotation);
            lockstep.WriteVector3(entityData.scale);

            if (!noTransformSync) // For both serialization and exports.
            { } // Weird empty block just to match the conditions in the deserialize function exactly.
            else if (!isExport)
                lockstep.WriteSmallUInt(entityData.lastKnownTransformStateTick);
            else
                lockstep.WriteSmallUInt(lockstep.CurrentTick - entityData.lastKnownTransformStateTick); // ticksAgo
        }

        private void DeserializeTransformValues(EntityData entityData, bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  DeserializeTransformValues");
#endif
            entityData.position = lockstep.ReadVector3();
            entityData.rotation = lockstep.ReadQuaternion();
            entityData.scale = lockstep.ReadVector3();

            if (!entityData.noTransformSync)
                entityData.lastKnownTransformStateTick = 0u;
            else if (!isImport)
                entityData.lastKnownTransformStateTick = lockstep.ReadSmallUInt();
            else
            {
                uint ticksAgo = lockstep.ReadSmallUInt();
                uint currentTick = lockstep.CurrentTick;
                entityData.lastKnownTransformStateTick = ticksAgo > currentTick ? 0u : currentTick - ticksAgo;
            }
            entityData.transformSyncController = null; // Systems must restore and set this on deserialization.
            // See SetTransformSyncControllerDueToDeserialization.
        }

        public void Serialize(EntityData entityData, bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  Serialize");
#endif
            lockstep.WriteFlags(entityData.noTransformSync, entityData.hidden);
            SerializeTransformValues(entityData, isExport);
            entitySystem.WritePlayerDataRef(entityData.CreatedByPlayerData);
            entitySystem.WritePlayerDataRef(entityData.LastUserPlayerData);

            EntityData parentEntity = entityData.parentEntity;
            EntityData[] childEntities = entityData.childEntities;
            lockstep.WriteSmallUInt(parentEntity == null ? 0u : parentEntity.id);
            lockstep.WriteSmallUInt((uint)childEntities.Length);
            foreach (EntityData child in childEntities)
                lockstep.WriteSmallUInt(child.id);

            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            if (isExport)
                lockstep.WriteSmallUInt((uint)allExtensionData.Length);
            foreach (EntityExtensionData extensionData in allExtensionData)
                lockstep.WriteCustomClass(extensionData, isExport);
        }

        public void Deserialize(EntityData entityData, bool isImport, uint importedDataVersion)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  Deserialize");
#endif
            lockstep.ReadFlags(out entityData.noTransformSync, out entityData.hidden);
            DeserializeTransformValues(entityData, isImport);
            entityData.CreatedByPlayerData = entitySystem.ReadPlayerDataRef(isImport);
            entityData.LastUserPlayerData = entitySystem.ReadPlayerDataRef(isImport);

            entityData.unresolvedParentEntityId = lockstep.ReadSmallUInt();
            int childEntitiesLength = (int)lockstep.ReadSmallUInt();
            uint[] unresolvedChildEntitiesIds = new uint[childEntitiesLength];
            entityData.unresolvedChildEntitiesIds = unresolvedChildEntitiesIds;
            for (int i = 0; i < childEntitiesLength; i++)
                unresolvedChildEntitiesIds[i] = lockstep.ReadSmallUInt();

            if (!isImport)
            {
                ResolveParentEntityId(entityData);
                ResolveChildEntityIds(entityData);
                if (!entityData.isInitialized)
                    InitAllExtensionDataBeforeDeserialization(entityData);
                DeserializeAllExtensionData(entityData);
            }
            else
            {
                ResolveImportedParentEntityId(entityData);
                ResolveImportedChildEntityIds(entityData);
                entityData.latencyHiddenUniqueIds.Clear();
                if (!entityData.isInitialized)
                    InitAllExtensionDataBeforeDeserialization(entityData);
                ImportAllExtensionData(entityData);
            }

            if (entityData.noTransformSync && entityData.transformSyncController == null)
            {
                Debug.LogError($"[EntitySystem] An EntityData had {nameof(EntityData.noTransformSync)} set to true during "
                    + $"deserialization, however no system restored the transform sync controller which is "
                    + $"responsible for managing this EntityData. Use {nameof(SetTransformSyncControllerDueToDeserialization)} "
                    + $"inside of the Deserialize function of an EntityExtensionData.");
                // To prevent runtime errors at least, but this is not guaranteed to
                // actually resolve this error case in a game state safe manner.
                entityData.noTransformSync = false;
                entityData.lastKnownTransformStateTick = 0u;
            }
        }

        private void ResolveParentEntityId(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ResolveParentEntityId");
#endif
            entitySystem.TryGetEntityData(entityData.unresolvedParentEntityId, out entityData.parentEntity);
            entityData.unresolvedParentEntityId = 0u;
        }

        private void ResolveChildEntityIds(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ResolveChildEntityIds");
#endif
            uint[] unresolvedChildEntitiesIds = entityData.unresolvedChildEntitiesIds;
            int count = unresolvedChildEntitiesIds.Length;
            EntityData[] childEntities = new EntityData[count];
            entityData.childEntities = childEntities;
            for (int i = 0; i < count; i++)
                childEntities[i] = entitySystem.GetEntityData(unresolvedChildEntitiesIds[i]);
            entityData.unresolvedChildEntitiesIds = null;
        }

        private void ResolveImportedParentEntityId(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ResolveImportedParentEntityId");
#endif
            entitySystem.TryGetRemappedImportedEntityData(entityData.unresolvedParentEntityId, out entityData.parentEntity);
            entityData.unresolvedParentEntityId = 0u;
        }

        private void ResolveImportedChildEntityIds(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ResolveImportedChildEntityIds");
#endif
            uint[] unresolvedChildEntitiesIds = entityData.unresolvedChildEntitiesIds;
            EntityData[] childEntities = new EntityData[unresolvedChildEntitiesIds.Length];
            entityData.childEntities = childEntities;
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
            entityData.unresolvedChildEntitiesIds = null;
        }

        private void DeserializeAllExtensionData(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  DeserializeAllExtensionData");
#endif
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                lockstep.ReadCustomClass(extensionData, isImport: false);
        }

        private void ImportAllExtensionData(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  ImportAllExtensionData");
#endif
            int length = (int)lockstep.ReadSmallUInt();
            if (IsDummyEntityDataForImport(entityData))
            {
                for (int i = 0; i < length; i++)
                    lockstep.SkipCustomClass(out var discard1, out var discard2); // Cannot use 'out _'.
                return;
            }
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            for (int i = 0; i < length; i++)
            {
                string newExtensionClassName = entityData.importedMetadata.resolvedExtensionClassNames[i];
                if (newExtensionClassName == null)
                {
                    lockstep.SkipCustomClass(out var discard1, out var discard2); // Cannot use 'out _'.
                    continue;
                }
                int index = entityData.importedMetadata.resolvedExtensionIndexes[i];
                EntityExtensionData extensionData = allExtensionData[index];
                if (!lockstep.ReadCustomClass(extensionData, isImport: true))
                    extensionData.OnImportedWithoutDeserialization();
            }
        }

        // Disgusting micro optimization, by using these we ultimately save on one GetProgramVariable call for each of them.
        private Vector3 incomingPosition;
        private Quaternion incomingRotation;
        private Vector3 incomingScale;

        /// <summary>
        /// <para>Send function is here <see cref="Entity.SendTransformChangeIA"/>.</para>
        /// </summary>
        public void OnTransformChangeIA(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  OnTransformChangeIA");
#endif
            if (entityData.noTransformSync)
            {
                MarkLatencyHiddenUniqueIdAsProcessed(entityData);
                return;
            }

            lockstep.ReadFlags(
                out bool positionChange, out bool discontinuousPositionChange,
                out bool rotationChange, out bool discontinuousRotationChange,
                out bool scaleChange, out bool discontinuousScaleChange);

            if (positionChange)
            {
                incomingPosition = lockstep.ReadVector3();
                entityData.position = incomingPosition;
            }

            if (rotationChange)
            {
                incomingRotation = lockstep.ReadQuaternion();
                entityData.rotation = incomingRotation;
            }

            if (scaleChange)
            {
                incomingScale = lockstep.ReadVector3();
                entityData.scale = incomingScale;
            }

            Entity entity = entityData.entity;
            if (!ShouldApplyReceivedIAToLatencyState(entityData) || entity == null)
                return;

            Transform entityTransform = entity.transform;

            if (positionChange)
            {
                if (!discontinuousPositionChange)
                    interpolation.LerpWorldPosition(entityTransform, incomingPosition, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelPositionInterpolation(entityTransform);
                    entityTransform.position = incomingPosition;
                }
            }

            if (rotationChange)
            {
                if (!discontinuousRotationChange)
                    interpolation.LerpWorldRotation(entityTransform, incomingRotation, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelRotationInterpolation(entityTransform);
                    entityTransform.rotation = incomingRotation;
                }
            }

            if (scaleChange)
            {
                if (!discontinuousScaleChange)
                    interpolation.LerpLocalScale(entityTransform, incomingScale, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelScaleInterpolation(entityTransform);
                    entityTransform.localScale = incomingScale;
                }
            }
        }
    }
}
