using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityData : WannaBeClass
    {
        [HideInInspector][SingletonReference] public LockstepAPI lockstep;
        [HideInInspector][SingletonReference] public EntitySystem entitySystem;
        [HideInInspector][SingletonReference] public InterpolationManager interpolation;
        [HideInInspector][SingletonReference] public PlayerDataManagerAPI playerDataManager;
        /// <summary>
        /// <para>Negative 1 means that the entity has been destroyed.</para>
        /// <para>Not part of the public api.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        [System.NonSerialized] public int instanceIndex;
        [System.NonSerialized] public EntityPrototype entityPrototype;
        /// <summary>
        /// <para>Not game state safe.</para>
        /// </summary>
        [System.NonSerialized] public Entity entity;
        /// <summary>
        /// <para>Once <see langword="true"/>, <see langword="true"/> forever.</para>
        /// <para>Not game state safe.</para>
        /// </summary>
        [System.NonSerialized] public bool entityIsDestroyed;
        [System.NonSerialized] public bool wasPreInstantiated = false;
        [System.NonSerialized] public bool isInitialized = false;
        [System.NonSerialized] public ulong uniqueId;
        [System.NonSerialized] public uint id;
        [System.NonSerialized] public bool noTransformSync;
        /// <summary>
        /// <para>Could still be <see langword="null"/> even when <see cref="noTransformSync"/> is
        /// <see langword="true"/> inside of entity extension data deserialize functions, when the entity
        /// extension data responsible for restoring it has not run yet.</para>
        /// </summary>
        [System.NonSerialized] public EntityTransformController transformSyncController;
        [System.NonSerialized] public Vector3 position;
        [System.NonSerialized] public Quaternion rotation;
        [System.NonSerialized] public Vector3 scale;
        [System.NonSerialized] public uint lastKnownTransformStateTick;
        /// <summary>
        /// <para>Can be <see langword="null"/>.</para>
        /// </summary>
        [System.NonSerialized] public EntitySystemPlayerData createdByPlayerData;
        /// <summary>
        /// <para>Can be <see langword="null"/>.</para>
        /// </summary>
        [System.NonSerialized] public EntitySystemPlayerData lastUserPlayerData;
        [System.NonSerialized] public bool hidden;
        [System.NonSerialized] public EntityData parentEntity;
        [System.NonSerialized] public EntityData[] childEntities = new EntityData[0];
        // Having serialized fields in WannaBeClasses is a very bad idea, as they could end up having stale
        // previous default values. It's fine in this case as the value gets overwritten for newly created
        // entity data, and the pre instantiated ones get populated through editor scripting.
        [HideInInspector] public EntityExtensionData[] allExtensionData;

        [System.NonSerialized] public uint unresolvedParentEntityId;
        [System.NonSerialized] public uint[] unresolvedChildEntitiesIds;

        private bool IsDummyEntityDataForImport => entityPrototype == null;
        [System.NonSerialized] public EntityPrototypeMetadata importedMetadata;

        private DataDictionary latencyUniqueIdLut = new DataDictionary();

        public const string OnTransformSyncControlLostEvent = "OnTransformSyncControlLost";
        public const string OnLatencyTransformSyncControlLostEvent = "OnLatencyTransformSyncControlLost";
        public const string ControlledEntityDataField = "controlledEntityData";

        public Vector3 LastKnownPosition
            => transformSyncController != null && transformSyncController.TryGetGameStatePosition(this, out Vector3 position)
                ? position
                : this.position;
        public Quaternion LastKnownRotation
            => transformSyncController != null && transformSyncController.TryGetGameStateRotation(this, out Quaternion rotation)
                ? rotation
                : this.rotation;
        public Vector3 LastKnownScale
            => transformSyncController != null && transformSyncController.TryGetGameStateScale(this, out Vector3 scale)
                ? scale
                : this.scale;

        public EntityData WannaBeConstructor(EntityPrototype entityPrototype, ulong uniqueId, uint id)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  WannaBeConstructor - entityPrototype.PrototypeName: {(entityPrototype == null ? "<null>" : entityPrototype.PrototypeName)}, uniqueId: 0x{uniqueId:x16}, id: {id}");
#endif
            this.entityPrototype = entityPrototype;
            this.uniqueId = uniqueId;
            this.id = id;

            if (IsDummyEntityDataForImport)
                return this;

            string[] extensionClassNames = entityPrototype.ExtensionDataClassNames;
            int length = extensionClassNames.Length;
            if (allExtensionData.Length == length) // Already populated for pre instantiated EntityData.
                for (int i = 0; i < length; i++)
                    allExtensionData[i].WannaBeConstructor(i, this);
            else
            {
                allExtensionData = new EntityExtensionData[length];
                for (int i = 0; i < length; i++)
                    allExtensionData[i] = EntityExtensionDataStatics.New(WannaBeClasses, extensionClassNames[i], i, this);
            }

            return this;
        }

        public override void WannaBeDestructor()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  WannaBeDestructor");
#endif
            if (allExtensionData == null)
                return;
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.DecrementRefsCount();
        }

        public void SendDestroyEntityIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  SendDestroyEntityIA");
#endif
            entitySystem.SendDestroyEntityIA(this);
        }

        public void SetEntity(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  SetEntity");
#endif
            this.entity = entity;
            entity.entityData = this;
        }

        public void InitFromDefault(
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            EntitySystemPlayerData createdByPlayerData,
            EntitySystemPlayerData lastUserPlayerData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromDefault");
            if (isInitialized)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromDefault "
                    + $"EntityData which has already been initialized.");
#endif
            isInitialized = true;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.createdByPlayerData = createdByPlayerData;
            this.lastUserPlayerData = lastUserPlayerData;
            hidden = false;
            parentEntity = null;

            int length = allExtensionData.Length;
            EntityExtension[] defaultExtensions = entityPrototype.DefaultEntityInst.extensions;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromDefault(defaultExtensions[i]);
        }

        public void InitFromPreInstantiated(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromPreInstantiated");
            if (isInitialized)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromPreInstantiated "
                    + $"EntityData which has already been initialized.");
#endif
            isInitialized = true;
            Transform t = entity.transform;
            position = t.position;
            rotation = t.rotation;
            scale = t.localScale;
            createdByPlayerData = null;
            lastUserPlayerData = null;
            hidden = false;
            parentEntity = null;

            EntityExtension[] extensions = entity.extensions;
            int length = allExtensionData.Length;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromPreInstantiated(extensions[i]);
        }

        private void InitAllExtensionDataBeforeDeserialization()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  InitAllExtensionDataBeforeDeserialization");
            if (isInitialized)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitAllExtensionDataBeforeDeserialization "
                    + $"EntityData which has already been initialized.");
#endif
            isInitialized = true;
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.InitBeforeDeserialization();
        }

        public void OnEntityDataCreated()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  OnEntityDataCreated");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnEntityExtensionDataCreated();
        }

        public void OnAssociatedWithEntity()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  OnAssociatedWithEntity");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnAssociatedWithExtension();
        }

        public void OnDisassociateFromEntity()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  OnDisassociateFromEntity");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnDisassociateFromExtension();
        }

        public void OnEntityDataDestroyed()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  OnEntityDataDestroyed");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnEntityExtensionDataDestroyed();
        }

        public bool RegisterLatencyHiddenUniqueId(ulong uniqueId)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  RegisterLatencyHiddenUniqueId - uniqueId: 0x{uniqueId:x16}");
#endif
            if (uniqueId == 0uL)
                return false;
            latencyUniqueIdLut.Add(uniqueId, true);
            return true;
        }

        /// <summary>
        /// <para>Make all changes to the game state before calling this function, modify the latency state
        /// after, but only if this returned <see langword="true"/>.</para>
        /// </summary>
        /// <returns></returns>
        public bool ShouldApplyReceivedIAToLatencyState()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  ShouldApplyReceivedIAToLatencyState - uniqueId: 0x{lockstep.SendingUniqueId:x16}, latencyUniqueIdLut.Count: {latencyUniqueIdLut.Count}");
#endif
            if (latencyUniqueIdLut.Count == 0)
                return true;
            if (latencyUniqueIdLut.Remove(lockstep.SendingUniqueId)) // Was already applied to latency state, stay in latency state.
                return false;
            // The latency state is desynced from the game state, however an input action which has not been
            // applied to the game state has been received in between input actions which have already been
            // applied to the latency state. Cannot just apply this IA to the latency state, because that way
            // IAs got applied to the latency state in different order than the game state, so reset the
            // latency state to match the game state entirely instead. This undoes some IAs which had already
            // been applied to the latency state and they are going to get applied again whenever the
            // associated IA gets run in the game state.
            latencyUniqueIdLut.Clear();
            if (entity != null)
                entity.ApplyEntityData();
            return false; // Already got applied by the above.
        }

        /// <summary>
        /// <para>Do not modify the game state nor latency state after calling this function.</para>
        /// </summary>
        public void MarkLatencyHiddenUniqueIdAsProcessed()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  MarkLatencyHiddenUniqueIdAsProcessed");
#endif
            ShouldApplyReceivedIAToLatencyState(); // Literally the same logic.
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
        /// <returns><see langword="true"/> if the latency state had indeed diverged.</returns>
        public bool ResetLatencyStateIfItDiverged()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  ResetLatencyStateIfItDiverged");
#endif
            if (latencyUniqueIdLut.Count == 0)
                return false;
            latencyUniqueIdLut.Clear();
            if (entity != null)
                entity.ApplyEntityData();
            return true;
        }

        public void WritePotentiallyUnknownTransformValues()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityData  WritePotentiallyUnknownTransformValues");
#endif
            if (!noTransformSync // At the time of sending the data was known, so we will have close to up to date values for sure.
                || entity == null // May or may not be unknown by the current transform controller, but we couldn't send any transform data anyway without an entity transform.
                || !entity.noTransformSync // Control has already been given back in the latency state, we are going to have up to date values.
                || lockstep.CurrentTick - lastKnownTransformStateTick <= LockstepAPI.TickRate) // We knew transform values 1 second ago, that'll be good enough.
            {
                lockstep.WriteByte(0);
                return;
            }

            // Use latency state controller as it will most likely be the controller at the time of receiving data.
            EntityTransformController controller = entity.transformSyncController;
            bool unknownPosition = !controller.TryGetGameStatePosition(this, out var discard1); // Cannot use 'out _'.
            bool unknownRotation = !controller.TryGetGameStateRotation(this, out var discard2); // Cannot use 'out _'.
            bool unknownScale = !controller.TryGetGameStateScale(this, out var discard3); // Cannot use 'out _'.

            lockstep.WriteFlags(unknownPosition, unknownRotation, unknownScale);
            if (unknownPosition)
                lockstep.WriteVector3(position);
            if (unknownRotation)
                lockstep.WriteQuaternion(rotation);
            if (unknownScale)
                lockstep.WriteVector3(scale);
        }

        public void ReadPotentiallyUnknownTransformValues()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityData  ReadPotentiallyUnknownTransformValues");
#endif
            lockstep.ReadFlags(out bool unknownPosition, out bool unknownRotation, out bool unknownScale);
            if (unknownPosition)
                position = lockstep.ReadVector3();
            if (unknownRotation)
                rotation = lockstep.ReadQuaternion();
            if (unknownScale)
                scale = lockstep.ReadVector3();
        }

        public void TakeControlOfTransformSync(EntityTransformController controller)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityData  TakeControlOfTransformSync");
#endif
            if (!noTransformSync)
            {
                noTransformSync = true;
                lastKnownTransformStateTick = lockstep.CurrentTick;
                transformSyncController = controller;
            }
            else if (controller != transformSyncController)
            {
                EntityTransformController prevController = transformSyncController;
                transformSyncController = controller;
                prevController.OnControlTakenOver(this, controller);
            }
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfTransformSync(
            EntityTransformController releasingController,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float interpolationDuration = Entity.TransformChangeInterpolationDuration)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityData  GiveBackControlOfTransformSync");
#endif
            if (!noTransformSync || (releasingController != null && transformSyncController != releasingController))
                return;
            noTransformSync = false;
            lastKnownTransformStateTick = 0u;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            EntityTransformController prevController = transformSyncController;
            transformSyncController = null;
            if (releasingController == null)
                prevController.OnControlLost(this);
        }

        public void SetTransformSyncControllerDueToDeserialization(EntityTransformController controller)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityData  SetTransformSyncControllerDueToDeserialization");
#endif
            if (!noTransformSync)
            {
                Debug.LogError($"[EntitySystem] Attempt to SetTransformSyncControllerDueToDeserialization when "
                    + $"noTransformSync is false, aka should not be any transform sync controller.");
                return;
            }
            if (transformSyncController != null)
            {
                Debug.LogError($"[EntitySystem] Attempt to SetTransformSyncControllerDueToDeserialization when "
                    + $"a different system has already set a controller. One of these systems is misbehaving.");
                return;
            }
            transformSyncController = controller;
        }

        private void SerializeTransformValues(bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  SerializeTransformValues");
#endif
            if (!noTransformSync) // For both serialization and exports.
            {
                lockstep.WriteVector3(position);
                lockstep.WriteQuaternion(rotation);
                lockstep.WriteVector3(scale);
                return;
            }
            if (!isExport)
            {
                lockstep.WriteSmallUInt(lastKnownTransformStateTick);
                return;
            }
            if (entity == null)
            {
                lockstep.WriteVector3(position);
                lockstep.WriteQuaternion(rotation);
                lockstep.WriteVector3(scale);
                lockstep.WriteSmallUInt(lockstep.CurrentTick - lastKnownTransformStateTick); // ticksAgo
                return;
            }
            // Exports can actually use non game state safe data.
            // Imports are going to turn that data into game state safe data anyway.
            Transform entityTransform = entity.transform;
            lockstep.WriteVector3(entityTransform.position);
            lockstep.WriteQuaternion(entityTransform.rotation);
            lockstep.WriteVector3(entityTransform.localScale);
            lockstep.WriteSmallUInt(0u); // ticksAgo
        }

        private void DeserializeTransformValues(bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeTransformValues");
#endif
            position = lockstep.ReadVector3();
            rotation = lockstep.ReadQuaternion();
            scale = lockstep.ReadVector3();
            if (!noTransformSync)
                lastKnownTransformStateTick = 0u;
            else if (!isImport)
                lastKnownTransformStateTick = lockstep.ReadSmallUInt();
            else
            {
                uint ticksAgo = lockstep.ReadSmallUInt();
                uint currentTick = lockstep.CurrentTick;
                lastKnownTransformStateTick = ticksAgo > currentTick ? 0u : currentTick - ticksAgo;
            }
            transformSyncController = null; // Systems must restore and set this on deserialization.
            // See SetTransformSyncControllerDueToDeserialization.
        }

        public void WritePlayerData(EntitySystemPlayerData playerData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  WritePlayerData");
#endif
            lockstep.WriteSmallUInt(playerData == null ? 0u : playerData.core.persistentId);
        }

        public EntitySystemPlayerData ReadPlayerData(bool isImport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  ReadPlayerData");
#endif
            uint persistentId = lockstep.ReadSmallUInt();
            if (persistentId == 0u)
                return null;
            if (isImport)
                persistentId = playerDataManager.GetPersistentIdFromImportedId(persistentId);
            return playerDataManager.GetPlayerDataForPersistentId<EntitySystemPlayerData>(nameof(EntitySystemPlayerData), persistentId);
        }

        public void Serialize(bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  Serialize");
#endif
            lockstep.WriteFlags(noTransformSync, hidden);
            SerializeTransformValues(isExport);
            WritePlayerData(createdByPlayerData);
            WritePlayerData(lastUserPlayerData);
            lockstep.WriteSmallUInt(parentEntity == null ? 0u : parentEntity.id);
            lockstep.WriteSmallUInt((uint)childEntities.Length);
            foreach (EntityData child in childEntities)
                lockstep.WriteSmallUInt(child.id);
            if (isExport)
                lockstep.WriteSmallUInt((uint)allExtensionData.Length);
            foreach (EntityExtensionData extensionData in allExtensionData)
                lockstep.WriteCustomClass(extensionData);
        }

        public void Deserialize(bool isImport, uint importedDataVersion)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  Deserialize");
#endif
            lockstep.ReadFlags(out noTransformSync, out hidden);
            DeserializeTransformValues(isImport);
            createdByPlayerData = ReadPlayerData(isImport);
            lastUserPlayerData = ReadPlayerData(isImport);
            if (createdByPlayerData != null)
                createdByPlayerData.GainCreated(this);
            if (lastUserPlayerData != null)
                lastUserPlayerData.GainLastUsed(this);
            unresolvedParentEntityId = lockstep.ReadSmallUInt();
            int childEntitiesLength = (int)lockstep.ReadSmallUInt();
            unresolvedChildEntitiesIds = new uint[childEntitiesLength];
            for (int i = 0; i < childEntitiesLength; i++)
                unresolvedChildEntitiesIds[i] = lockstep.ReadSmallUInt();
            if (isImport)
            {
                ResolveImportedParentEntityId();
                ResolveImportedChildEntityIds();
                latencyUniqueIdLut.Clear();
                if (!isInitialized)
                    InitAllExtensionDataBeforeDeserialization();
                ImportAllExtensionData();
            }
            else
            {
                if (!isInitialized)
                    InitAllExtensionDataBeforeDeserialization();
                DeserializeAllExtensionData();
            }

            if (noTransformSync && transformSyncController == null)
            {
                Debug.LogError($"[EntitySystem] An EntityData had {nameof(noTransformSync)} set to true during "
                    + $"deserialization, however no system restored the transform sync controller which is "
                    + $"responsible for managing this EntityData. Use {nameof(SetTransformSyncControllerDueToDeserialization)} "
                    + $"inside of the Deserialize function of an EntityExtensionData.");
                // To prevent runtime errors at least, but this is not guaranteed to
                // actually resolve this error case in a game state safe manner.
                noTransformSync = false;
                lastKnownTransformStateTick = 0u;
            }
        }

        private void ResolveImportedParentEntityId()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  ResolveImportedParentEntityId");
#endif
            entitySystem.TryGetRemappedImportedEntityData(unresolvedParentEntityId, out parentEntity);
            unresolvedParentEntityId = 0u;
        }

        private void ResolveImportedChildEntityIds()
        {
#if ENTITY_SYSTEM_DEBUG
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
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeAllExtensionData");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                lockstep.ReadCustomClass(extensionData);
        }

        private void ImportAllExtensionData()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  ImportAllExtensionData");
#endif
            int length = (int)lockstep.ReadSmallUInt();
            if (IsDummyEntityDataForImport)
            {
                for (int i = 0; i < length; i++)
                    lockstep.SkipCustomClass(out var discard1, out var discard2); // Cannot use 'out _'.
                return;
            }
            for (int i = 0; i < length; i++)
            {
                string newExtensionClassName = importedMetadata.resolvedExtensionClassNames[i];
                if (newExtensionClassName == null)
                {
                    lockstep.SkipCustomClass(out var discard1, out var discard2); // Cannot use 'out _'.
                    continue;
                }
                int index = importedMetadata.resolvedExtensionIndexes[i];
                EntityExtensionData extensionData = allExtensionData[index];
                if (!lockstep.ReadCustomClass(extensionData))
                    extensionData.OnImportedWithoutDeserialization();
            }
        }

        /// <summary>
        /// <para>Send function is here <see cref="Entity.SendTransformChangeIA"/>.</para>
        /// </summary>
        public void OnTransformChangeIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  OnTransformChangeIA");
#endif
            if (noTransformSync)
            {
                MarkLatencyHiddenUniqueIdAsProcessed();
                return;
            }

            lockstep.ReadFlags(
                out bool positionChange, out bool discontinuousPositionChange,
                out bool rotationChange, out bool discontinuousRotationChange,
                out bool scaleChange, out bool discontinuousScaleChange);

            if (positionChange)
                position = lockstep.ReadVector3();
            if (rotationChange)
                rotation = lockstep.ReadQuaternion();
            if (scaleChange)
                scale = lockstep.ReadVector3();

            if (!ShouldApplyReceivedIAToLatencyState() || entity == null)
                return;

            Transform entityTransform = entity.transform;

            if (positionChange)
            {
                if (!discontinuousPositionChange)
                    interpolation.LerpWorldPosition(entityTransform, position, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelPositionInterpolation(entityTransform);
                    entityTransform.position = position;
                }
            }

            if (rotationChange)
            {
                if (!discontinuousRotationChange)
                    interpolation.LerpWorldRotation(entityTransform, rotation, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelRotationInterpolation(entityTransform);
                    entityTransform.rotation = rotation;
                }
            }

            if (scaleChange)
            {
                if (!discontinuousScaleChange)
                    interpolation.LerpLocalScale(entityTransform, scale, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelScaleInterpolation(entityTransform);
                    entityTransform.localScale = scale;
                }
            }
        }
    }

    public static class EntityDataExtensions
    {
        public static int IndexOfExtensionData(this EntityData entityData, string extensionDataClassName, int startIndex = 0)
            => System.Array.IndexOf(entityData.entityPrototype.ExtensionDataClassNames, extensionDataClassName, startIndex);

        public static EntityExtensionData GetExtensionDataDynamic(this EntityData entityData, string extensionDataClassName, int startIndex = 0)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  GetExtensionDataDynamic");
#endif
            int extensionIndex = System.Array.IndexOf(entityData.entityPrototype.ExtensionDataClassNames, extensionDataClassName, startIndex);
            return extensionIndex < 0 ? null : entityData.allExtensionData[extensionIndex];
        }

        public static T GetExtensionData<T>(this EntityData entityData, string extensionDataClassName, int startIndex = 0)
            where T : EntityExtensionData
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  GetExtensionData");
#endif
            // Same as GetExtensionDataDynamic.
            int extensionIndex = System.Array.IndexOf(entityData.entityPrototype.ExtensionDataClassNames, extensionDataClassName, startIndex);
            return (T)(extensionIndex < 0 ? null : entityData.allExtensionData[extensionIndex]);
        }
    }
}
