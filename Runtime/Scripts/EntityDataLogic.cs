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
        [HideInInspector][SerializeField][SingletonReference] private EntitySystem entitySystem;
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
            entityData.parentEntityData = null;

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
            entityData.position = t.localPosition;
            entityData.rotation = t.localRotation;
            entityData.scale = t.localScale;
            // These 2 are null anyway, but just for clarity.
            // And it is semantically correct to use the property setter here, this is a game state safe event.
            entityData.CreatedByPlayerData = null;
            entityData.LastUserPlayerData = null;
            entityData.hidden = false;
            entityData.parentEntityData = null;

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
            ClearParentEntity(entityData);
            ClearChildEntities(entityData);
            foreach (EntityExtensionData extensionData in entityData.allExtensionData)
                extensionData.OnEntityExtensionDataDestroyed();
        }

        private void ClearParentEntity(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityDataLogic  ClearParentEntity");
#endif
            EntityData parentEntityData = entityData.parentEntityData;
            if (parentEntityData == null)
                return;
            RemoveFromChildEntities(parentEntityData, entityData);
            entityData.parentEntityData = null;
        }

        private void ClearChildEntities(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityDataLogic  ClearChildEntities");
#endif
            EntityData[] childEntities = entityData.childEntities;
            int childEntitiesCount = entityData.childEntitiesCount;
            for (int i = 0; i < childEntitiesCount; i++)
                childEntities[i].parentEntityData = null;
            entityData.childEntitiesCount = 0;
        }

        private void AddToChildEntities(EntityData entityData, EntityData childEntityData)
        {
            ArrList.Add(ref entityData.childEntities, ref entityData.childEntitiesCount, childEntityData);
        }

        private void RemoveFromChildEntities(EntityData entityData, EntityData childEntityData)
        {
            ArrList.Remove(ref entityData.childEntities, ref entityData.childEntitiesCount, childEntityData);
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
                entity.ApplyEntityData();
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
                entity.ApplyEntityData();
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
                entity.ApplyEntityData();
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
                lockstep.WriteVector3(entityTransform.localPosition);
            if (unknownRotation)
                lockstep.WriteQuaternion(entityTransform.localRotation);
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
                lockstep.WriteVector3(entityTransform.localPosition);
                lockstep.WriteQuaternion(entityTransform.localRotation);
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

        private void SerializeParentEntity(EntityData entityData, bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  SerializeParentEntity");
#endif
            EntityData parentEntityData = entityData.parentEntityData;
            lockstep.WriteSmallUInt(parentEntityData == null ? EntitySystemAPI.InvalidId : parentEntityData.id);
            if (!isExport || parentEntityData == null)
                return;

            Entity entity = entityData.entity;
            if (entity == null)
            {
                lockstep.WriteFlags(false); // worldLocationIsKnown.
                return;
            }
            lockstep.WriteFlags(true); // worldLocationIsKnown.
            Transform t = entity.transform;
            lockstep.WriteVector3(t.position);
            lockstep.WriteQuaternion(t.rotation);
        }

        private void DeserializeParentEntity(EntityData entityData, bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  DeserializeParentEntity");
#endif
            uint parentEntityId = lockstep.ReadSmallUInt();
            if (parentEntityId == EntitySystemAPI.InvalidId)
            {
                entityData.parentEntityData = null;
                return;
            }
            if (!isImport)
            {
                entitySystem.TryGetEntityData(parentEntityId, out entityData.parentEntityData);
                return;
            }

            lockstep.ReadFlags(out bool worldLocationIsKnown);

            if (entitySystem.TryGetRemappedImportedEntityData(parentEntityId, out entityData.parentEntityData))
            {
                if (!worldLocationIsKnown)
                    return;
                lockstep.ReadVector3(); // Discard.
                lockstep.ReadQuaternion(); // Discard.
                return;
            }

            // Had a parent entity when exporting but cannot resolve it when importing,
            // try to set the world position and rotation.
            if (!worldLocationIsKnown)
            {
                // This case here is actually really bad. The local position and rotation of the entity at the
                // time of export - relative to some entity which does not exist upon importing - will now be
                // used as the world position and rotation of the entity.
                // Arguably might as well destroy this entity at this point... right?
                // Maybe it should, yea, though world location being unknown is so unlikely that I do not care right now.
                // Realistically, the correct solution to this would be to calculate what the world location
                // should be at the time of exporting, based on the entity data parent hierarchy and their
                // positions, rotations and scales. Then the world location here would always be known.
                return;
            }

            entityData.position = lockstep.ReadVector3();
            entityData.rotation = lockstep.ReadQuaternion();
        }

        private void SerializeChildEntities(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  SerializeChildEntities");
#endif
            EntityData[] childEntities = entityData.childEntities;
            int childEntitiesCount = entityData.childEntitiesCount;
            lockstep.WriteSmallUInt((uint)childEntitiesCount);
            for (int i = 0; i < childEntitiesCount; i++)
                entitySystem.WriteEntityDataRef(childEntities[i]);
        }

        private void DeserializeChildEntities(EntityData entityData, bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  DeserializeChildEntities");
#endif
            int incomingChildEntitiesCount = (int)lockstep.ReadSmallUInt();
            EntityData[] childEntities = entityData.childEntities;
            int count = 0; // For imports this count may end up not matching the incoming count.
            for (int i = 0; i < incomingChildEntitiesCount; i++)
                if (entitySystem.TryReadEntityDataRef(out EntityData childData, isImport))
                    ArrList.Add(ref childEntities, ref count, childData);
            entityData.childEntities = childEntities;
            entityData.childEntitiesCount = count;
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

            SerializeParentEntity(entityData, isExport);
            SerializeChildEntities(entityData);

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

            DeserializeParentEntity(entityData, isImport);
            DeserializeChildEntities(entityData, isImport);

            if (!entityData.isInitialized)
                InitAllExtensionDataBeforeDeserialization(entityData);

            if (!isImport)
                DeserializeAllExtensionData(entityData);
            else
            {
                entityData.latencyHiddenUniqueIds.Clear();
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
                    interpolation.LerpLocalPosition(entityTransform, incomingPosition, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelPositionInterpolation(entityTransform);
                    entityTransform.localPosition = incomingPosition;
                }
            }

            if (rotationChange)
            {
                if (!discontinuousRotationChange)
                    interpolation.LerpLocalRotation(entityTransform, incomingRotation, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelRotationInterpolation(entityTransform);
                    entityTransform.localRotation = incomingRotation;
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

        public bool IsNestedChildOfInLS(EntityData entityData, EntityData potentialParent)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  IsNestedChildOfInLS");
#endif
            do
            {
                if (entityData == potentialParent)
                    return true;
                Entity entity = entityData.entity;
                entityData = entity != null ? entity.parentEntityData : entityData.parentEntityData;
            }
            while (entityData != null);
            return false;
        }

        public bool IsNestedChildOfInGS(EntityData entityData, EntityData potentialParent)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  IsNestedChildOfInGS");
#endif
            do
            {
                if (entityData == potentialParent)
                    return true;
                entityData = entityData.parentEntityData;
            }
            while (entityData != null);
            return false;
        }

        public void SendSetParentEntityIA(
            EntityData entityData,
            EntityData parentEntityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool doInterpolateLocally,
            bool doInterpolateRemotely)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  SendSetParentEntityIA");
#endif
            if (IsNestedChildOfInLS(parentEntityData, entityData))
                return;

            entitySystem.WriteEntityDataRef(entityData);
            entitySystem.WriteEntityDataRef(parentEntityData);

            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            lockstep.WriteVector3(scale);
            lockstep.WriteFlags(doInterpolateRemotely);

            ulong uniqueId = lockstep.SendInputAction(setParentEntityIAId);

            Entity entity = entityData.entity;
            if (entity == null || !entityData.RegisterLatencyHiddenUniqueId(uniqueId))
                return;

            entityLogic.SetParentEntityInLS(entity, parentEntityData, position, rotation, scale, doInterpolateLocally);
        }

        [HideInInspector][SerializeField] private uint setParentEntityIAId;
        [LockstepInputAction(nameof(setParentEntityIAId))]
        public void OnSetParentEntityIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  OnSetParentEntityIA");
#endif
            if (!entitySystem.TryReadEntityDataRef(out EntityData entityData, isImport: false))
                return;
            if (!entitySystem.TryReadEntityDataRef(out EntityData parentEntityData, isImport: false)
                || IsNestedChildOfInGS(parentEntityData, entityData))
            {
                // FIXME: Resetting latency state could cause a different loop in parents in the latency state.
                ResetLatencyStateBecauseIAGotAppliedDifferently(entityData);
                return;
            }

            Vector3 position = lockstep.ReadVector3();
            Quaternion rotation = lockstep.ReadQuaternion();
            Vector3 scale = lockstep.ReadVector3();
            lockstep.ReadFlags(out bool doInterpolate);

            SetParentEntityInGS(entityData, parentEntityData, position, rotation, scale);

            Entity entity = entityData.entity;
            if (!ShouldApplyReceivedIAToLatencyState(entityData) || entity == null)
                return;

            entityLogic.SetParentEntityInLS(entity, parentEntityData, position, rotation, scale, doInterpolate);
        }

        public void SetParentEntityInGS(
            EntityData entityData,
            EntityData parentEntityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  SetParentEntityInGS - with position, rotation and scale");
#endif
            entityData.position = position;
            entityData.rotation = rotation;
            entityData.scale = scale;

            SetParentEntityInGS(entityData, parentEntityData);
        }

        public void SetParentEntityInGS(EntityData entityData, EntityData parentEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityDataLogic  SetParentEntityInGS");
#endif
            EntityData prevParentEntityData = entityData.parentEntityData;
            if (prevParentEntityData != null)
                RemoveFromChildEntities(prevParentEntityData, entityData);

            entityData.parentEntityData = parentEntityData;
            if (parentEntityData)
                AddToChildEntities(parentEntityData, entityData);
        }
    }
}
