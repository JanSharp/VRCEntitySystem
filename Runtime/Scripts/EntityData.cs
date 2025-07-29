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
        [System.NonSerialized] public uint createdByPlayerId;
        [System.NonSerialized] public uint lastUserPlayerId;
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
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  WannaBeDestructor");
#endif
            if (allExtensionData == null)
                return;
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.DecrementRefsCount();
        }

        public void SendDestroyEntityIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SendDestroyEntityIA");
#endif
            entitySystem.SendDestroyEntityIA(this);
        }

        public void SetEntity(Entity entity)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SetEntity");
#endif
            this.entity = entity;
            entity.entityData = this;
        }

        public void InitFromDefault(Vector3 position, Quaternion rotation, Vector3 scale)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromDefault");
#endif
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            createdByPlayerId = 0u;
            lastUserPlayerId = 0u;
            hidden = false;
            parentEntity = null;
            InitAllExtensionDataFromDefault();
        }

        private void InitAllExtensionDataFromDefault()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitAllExtensionDataFromDefault");
#endif
            int length = allExtensionData.Length;
            EntityExtension[] defaultExtensions = entityPrototype.DefaultEntityInst.extensions;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromDefault(defaultExtensions[i]);
        }

        internal void InitFromPreInstantiated(Entity entity)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromPreInstantiated");
#endif
            Transform t = entity.transform;
            position = t.position;
            rotation = t.rotation;
            scale = t.localScale;
            createdByPlayerId = 0u;
            lastUserPlayerId = 0u;
            hidden = false;
            parentEntity = null;
            InitAllExtensionDataFromPreInstantiated(entity.extensions);
        }

        private void InitAllExtensionDataFromPreInstantiated(EntityExtension[] extensions)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitAllExtensionDataFromPreInstantiated");
#endif
            int length = allExtensionData.Length;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromPreInstantiated(extensions[i]);
        }

        public void OnEntityDataCreated()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnEntityDataCreated");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnEntityExtensionDataCreated();
        }

        public void OnAssociatedWithEntity()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnAssociatedWithEntity");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnAssociatedWithExtension();
        }

        public void OnDisassociateFromEntity()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnDisassociateFromEntity");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnDisassociateFromExtension();
        }

        public void OnEntityDataDestroyed()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnEntityDataDestroyed");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnEntityExtensionDataDestroyed();
        }

        public bool RegisterLatencyHiddenUniqueId(ulong uniqueId)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  RegisterLatencyHiddenUniqueId - uniqueId: 0x{uniqueId:x16}");
#endif
            if (uniqueId == 0uL)
                return false;
            latencyUniqueIdLut.Add(uniqueId, true);
            return true;
        }

        /// <summary>
        /// <para>Make all change to the game state before calling this function, modify the latency stat
        /// after, but only if this returned <see langword="true"/>.</para>
        /// </summary>
        /// <returns></returns>
        public bool ShouldApplyReceivedIAToLatencyState()
        {
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  MarkLatencyHiddenUniqueIdAsProcessed");
#endif
            ShouldApplyReceivedIAToLatencyState(); // Literally the same logic.
        }

        public void WritePotentiallyUnknownTransformValues()
        {
#if EntitySystemDebug
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
            bool unknownPosition = !controller.TryGetGameStatePosition(this, out var discord1); // Cannot use 'out _'.
            bool unknownRotation = !controller.TryGetGameStateRotation(this, out var discord2); // Cannot use 'out _'.
            bool unknownScale = !controller.TryGetGameStateScale(this, out var discord3); // Cannot use 'out _'.

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
#if EntitySystemDebug
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
#if EntitySystemDebug
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

            // if (latencyUniqueIdLut.Count == 0 && entity != null)
            //     entity.TakeControlOfTransformSync(controller);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfTransformSync(EntityTransformController releasingController, Vector3 position, Quaternion rotation, Vector3 scale, float interpolationDuration = Entity.TransformChangeInterpolationDuration)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] EntityData  GiveBackControlOfTransformSync");
#endif
            if (!noTransformSync || (releasingController != null && transformSyncController != releasingController))
            {
                // if (latencyUniqueIdLut.Count == 0 && entity != null)
                //     entity.GiveBackControlOfTransformSync(releasingController, position, rotation, scale, interpolationDuration);
                return;
            }
            noTransformSync = false;
            lastKnownTransformStateTick = 0u;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            EntityTransformController prevController = transformSyncController;
            transformSyncController = null;
            if (releasingController == null)
                prevController.OnControlLost(this);
            // if (latencyUniqueIdLut.Count == 0 && entity != null)
            //     entity.GiveBackControlOfTransformSync(releasingController, position, rotation, scale, interpolationDuration);
        }

        public void SetTransformSyncControllerDueToDeserialization(EntityTransformController controller)
        {
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SerializeTransformValues");
#endif
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            lockstep.WriteVector3(scale);
            if (!noTransformSync)
                return;
            if (!isExport)
                lockstep.WriteSmallUInt(lastKnownTransformStateTick);
            else
                lockstep.WriteSmallUInt(lockstep.CurrentTick - lastKnownTransformStateTick);
        }

        private void DeserializeTransformValue(bool isImport)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeTransformValue");
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

        public void Serialize(bool isExport)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Serialize");
#endif
            lockstep.WriteFlags(noTransformSync, hidden);
            SerializeTransformValues(isExport);
            lockstep.WriteSmallUInt(createdByPlayerId);
            lockstep.WriteSmallUInt(lastUserPlayerId);
            lockstep.WriteSmallUInt(parentEntity == null ? 0u : parentEntity.id);
            lockstep.WriteSmallUInt((uint)childEntities.Length);
            foreach (EntityData child in childEntities)
                lockstep.WriteSmallUInt(child.id);
            if (isExport)
                lockstep.WriteSmallUInt((uint)allExtensionData.Length);
            foreach (EntityExtensionData extensionData in allExtensionData)
                lockstep.WriteCustomNullableClass(extensionData); // TODO: why nullable? Probably because of exports, but even for non exports?
        }

        public void Deserialize(bool isImport, uint importedDataVersion)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Deserialize");
#endif
            lockstep.ReadFlags(out noTransformSync, out hidden);
            DeserializeTransformValue(isImport);
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
                latencyUniqueIdLut.Clear();
                ImportAllExtensionData();
            }
            else
                DeserializeAllExtensionData();

            if (noTransformSync && transformSyncController == null)
            {
                Debug.LogError($"[EntitySystem] An EntityData had noTransformSync set to true during "
                    + $"deserialization, however no system restored the transform sync controller which is "
                    + $"responsible for managing this EntityData. Use SetTransformSyncControllerDueToDeserialization "
                    + $"inside of the Deserialize function of an EntityExtensionData.");
                // To prevent runtime errors at least, but this is not guaranteed to
                // actually resolve this error case in a game state safe manner.
                noTransformSync = false;
                lastKnownTransformStateTick = 0u;
            }
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
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeAllExtensionData");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                lockstep.ReadCustomNullableClass(extensionData);
        }

        private void ImportAllExtensionData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  ImportAllExtensionData");
#endif
            int length = (int)lockstep.ReadSmallUInt();
            if (IsDummyEntityDataForImport)
            {
                for (int i = 0; i < length; i++)
                    lockstep.SkipCustomClass(out var discard1, out var discord2); // Cannot use 'out _'.
                return;
            }
            for (int i = 0; i < length; i++)
            {
                string newExtensionClassName = importedMetadata.resolvedExtensionClassNames[i];
                if (newExtensionClassName == null)
                {
                    lockstep.SkipCustomClass(out var discard1, out var discord2); // Cannot use 'out _'.
                    continue;
                }
                int index = importedMetadata.resolvedExtensionIndexes[i];
                EntityExtensionData extensionData = allExtensionData[index];
                if (!lockstep.ReadCustomNullableClass(extensionData))
                    extensionData.ImportedWithoutDeserialization();
            }
        }

        /// <summary>
        /// <para>Send function is here <see cref="Entity.SendTransformChangeIA"/>.</para>
        /// </summary>
        public void OnTransformChangeIA()
        {
#if EntitySystemDebug
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
                    interpolation.InterpolateWorldPosition(entityTransform, position, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelWorldPositionInterpolation(entityTransform);
                    entityTransform.position = position;
                }
            }

            if (rotationChange)
            {
                if (!discontinuousRotationChange)
                    interpolation.InterpolateWorldRotation(entityTransform, rotation, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelRotationInterpolation(entityTransform);
                    entityTransform.rotation = rotation;
                }
            }

            if (scaleChange)
            {
                if (!discontinuousScaleChange)
                    interpolation.InterpolateLocalScale(entityTransform, scale, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelLocalScaleInterpolation(entityTransform);
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  GetExtensionDataDynamic");
#endif
            int extensionIndex = System.Array.IndexOf(entityData.entityPrototype.ExtensionDataClassNames, extensionDataClassName, startIndex);
            return extensionIndex < 0 ? null : entityData.allExtensionData[extensionIndex];
        }

        public static T GetExtensionData<T>(this EntityData entityData, string extensionDataClassName, int startIndex = 0)
            where T : EntityExtensionData
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  GetExtensionData");
#endif
            // Same as GetExtensionDataDynamic.
            int extensionIndex = System.Array.IndexOf(entityData.entityPrototype.ExtensionDataClassNames, extensionDataClassName, startIndex);
            return (T)(extensionIndex < 0 ? null : entityData.allExtensionData[extensionIndex]);
        }
    }
}
