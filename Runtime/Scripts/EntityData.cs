using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityData : WannaBeClass
    {
        [HideInInspector][SingletonReference] public EntitySystemAPI entitySystem;
        [HideInInspector][SerializeField][SingletonReference] private Internal.EntityDataLogic logic;

        /// <summary>
        /// <para>Negative 1 means that the entity has been destroyed. But ensure to call
        /// <see cref="WannaBeClassExtensions.CheckLiveliness(WannaBeClass)"/> before checking for -1
        /// too, because if the class instance has been deleted, this gets reset to 0.</para>
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
        /// <para>Once <see langword="true"/>, <see langword="true"/> forever... until the
        /// <see cref="WannaBeClass"/> gets deconstructed and returned to the class instance pool.</para>
        /// <para>In other words, check <see cref="WannaBeClassExtensions.CheckLiveliness(WannaBeClass)"/>
        /// prior to checking this field.</para>
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
        /// <para>Weak reference, however guaranteed to always be a reference to an alive
        /// <see cref="WannaBeClass"/> instance when non <see langword="null"/>.</para>
        /// </summary>
        internal EntitySystemPlayerData createdByPlayerData;
        public const string InternalCreatedByPlayerDataFieldName = nameof(createdByPlayerData);
        /// <inheritdoc cref="createdByPlayerData"/>
        public EntitySystemPlayerData CreatedByPlayerData
        {
            get => createdByPlayerData;
            set
            {
                if (createdByPlayerData != null)
                    createdByPlayerData.LoseCreated(this);
                createdByPlayerData = value;
                if (createdByPlayerData != null)
                    createdByPlayerData.GainCreated(this);
            }
        }
        /// <inheritdoc cref="createdByPlayerData"/>
        internal EntitySystemPlayerData lastUserPlayerData;
        public const string InternalLastUserPlayerDataFieldName = nameof(lastUserPlayerData);
        /// <inheritdoc cref="lastUserPlayerData"/>
        public EntitySystemPlayerData LastUserPlayerData
        {
            get => lastUserPlayerData;
            set
            {
                if (lastUserPlayerData != null)
                    lastUserPlayerData.LoseLastUsed(this);
                lastUserPlayerData = value;
                if (lastUserPlayerData != null)
                    lastUserPlayerData.GainLastUsed(this);
            }
        }
        [System.NonSerialized] public bool hidden;

        [System.NonSerialized] public EntityData parentEntityData;
        [System.NonSerialized] public EntityData[] childEntities = new EntityData[ArrList.MinCapacity];
        [System.NonSerialized] public int childEntitiesCount = 0;

        // Having serialized fields in WannaBeClasses is a very bad idea, as they could end up having stale
        // previous default values. It's fine in this case as the value gets overwritten for newly created
        // entity data, and the pre instantiated ones get populated through editor scripting.
        [HideInInspector] public EntityExtensionData[] allExtensionData;

        [System.NonSerialized] public EntityPrototypeMetadata importedMetadata;

        internal DataDictionary latencyHiddenUniqueIds = new DataDictionary();

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

        public override bool WannaBeClassSupportsPooling => true;
        public override void ResetWannaBeClassToDefault()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  ResetWannaBeClassToDefault");
#endif
            instanceIndex = default;
            entityPrototype = default;
            entity = default;
            entityIsDestroyed = default;
            wasPreInstantiated = false;
            isInitialized = false;
            uniqueId = default;
            id = default;
            noTransformSync = default;
            transformSyncController = default;
            position = default;
            rotation = default;
            scale = default;
            lastKnownTransformStateTick = default;
            createdByPlayerData = default;
            lastUserPlayerData = default;
            hidden = default;
            parentEntityData = default;
            ArrList.Clear(ref childEntities, ref childEntitiesCount);
            allExtensionData = new EntityExtensionData[0]; // It is a serialized field, this is the default.
            importedMetadata = default;
            latencyHiddenUniqueIds.Clear();
        }

        public override void WannaBeDestructor() => logic.WannaBeDestructor(this);

        public void SendDestroyEntityIA(bool keepChildEntitiesAlive = false) => entitySystem.SendDestroyEntityIA(this, keepChildEntitiesAlive);

        public bool RegisterLatencyHiddenUniqueId(ulong uniqueId)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityData  RegisterLatencyHiddenUniqueId - uniqueId: 0x{uniqueId:x16}");
#endif
            if (uniqueId == 0uL)
                return false;
            latencyHiddenUniqueIds.Add(uniqueId, true);
            return true;
        }

        /// <summary>
        /// <para>Make all changes to the game state before calling this function, modify the latency state
        /// after, but only if this returned <see langword="true"/>.</para>
        /// </summary>
        /// <returns></returns>
        public bool ShouldApplyReceivedIAToLatencyState() => logic.ShouldApplyReceivedIAToLatencyState(this);

        public void MarkLatencyHiddenUniqueIdAsProcessed() => logic.ShouldApplyReceivedIAToLatencyState(this); // Literally the same logic.

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
        public bool ResetLatencyStateIfItDiverged() => logic.ResetLatencyStateIfItDiverged(this);

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
        public bool ResetLatencyStateBecauseIAGotAppliedDifferently() => logic.ResetLatencyStateBecauseIAGotAppliedDifferently(this);

        /// <summary>
        /// <para>Use this in cases where it is required for entity data to have up to date game state safe
        /// transform values for a given input action.</para>
        /// <para>This is required due to the potential existence of transform sync controllers which could
        /// make the current transform values of an entity unknown to the game state.</para>
        /// </summary>
        public void WritePotentiallyUnknownTransformValues() => logic.WritePotentiallyUnknownTransformValues(this);

        public void ReadPotentiallyUnknownTransformValues() => logic.ReadPotentiallyUnknownTransformValues(this);

        public void TakeControlOfTransformSync(EntityTransformController controller) => logic.TakeControlOfTransformSync(this, controller);

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfTransformSync(
            EntityTransformController releasingController,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            // The amount of wasted SetProgramVariable calls eww.
            // Udon and its stupid eternity it takes to instantiate scripts, causing me to do this.
            // I am disgusted.
            logic.GiveBackControlOfTransformSync(this, releasingController, position, rotation, scale);
        }

        public void SetTransformSyncControllerDueToDeserialization(EntityTransformController controller)
            => logic.SetTransformSyncControllerDueToDeserialization(this, controller);

        public bool IsNestedChildOfInLS(EntityData potentialParent) => logic.IsNestedChildOfInLS(this, potentialParent);
        public bool IsNestedChildOfInGS(EntityData potentialParent) => logic.IsNestedChildOfInGS(this, potentialParent);

        public void SendSetParentEntityIA(
            EntityData parentEntityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool doInterpolate)
        {
            // This is even worse.
            logic.SendSetParentEntityIA(this, parentEntityData, position, rotation, scale, doInterpolate, doInterpolate);
        }

        public void SetParentEntityInGS(
            EntityData parentEntityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            // lol.
            logic.SetParentEntityInGS(this, parentEntityData, position, rotation, scale);
        }

        public void SetParentEntityInGS(EntityData parentEntityData) => logic.SetParentEntityInGS(this, parentEntityData);
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
