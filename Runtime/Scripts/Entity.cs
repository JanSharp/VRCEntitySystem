using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp
{
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Entity : UdonSharpBehaviour
    {
        [HideInInspector][SingletonReference] public LockstepAPI lockstep;
        [HideInInspector][SingletonReference] public EntitySystemAPI entitySystem;
        [HideInInspector][SerializeField][SingletonReference] private Internal.EntityLogic logic;

        [System.NonSerialized] public EntityPrototype prototype;
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public int instanceIndex;

        [System.NonSerialized] public bool noTransformSync;
        [System.NonSerialized] public EntityTransformController transformSyncController;

        [Tooltip("Updated automatically when entering play mode or building the world.\n"
            + "Prefabs which are not used in a scene will not be updated. There's no need.")]
        public EntityExtension[] extensions;

        private bool transformChangeIAIsQueued = false;
        private bool flaggedForPositionChange = false;
        private bool flaggedForDiscontinuousPositionChange = false;
        private bool flaggedForRotationChange = false;
        private bool flaggedForDiscontinuousRotationChange = false;
        private bool flaggedForScaleChange = false;
        private bool flaggedForDiscontinuousScaleChange = false;
        private float timeAtLastTransformChangeIA = 0f;
        public const float TimeBetweenTransformChangeIAs = 0.2f;
        public const float TransformChangeInterpolationDuration = TimeBetweenTransformChangeIAs + 0.1f;
        public const float SetParentInterpolationDuration = TransformChangeInterpolationDuration;

        public const string OnLatencyParentEntityChangedEventName = "OnLatencyParentEntityChanged";

        public const string OnLatencyTransformSyncControllerChangedEventName = "OnLatencyTransformSyncControllerChanged";
        public const string OnLatencyTransformSyncControllerChangedLastFrameEventName = "OnLatencyTransformSyncControllerChangedLastFrame";
        internal bool flaggedForOnLatencyTransformSyncControllerChangedLastFrame = false;
        public void RaiseOnLatencyTransformSyncControllerChangedLastFrame()
        {
            flaggedForOnLatencyTransformSyncControllerChangedLastFrame = false;
            logic.RaiseRuntimeEvent(this, OnLatencyTransformSyncControllerChangedLastFrameEventName);
        }

        // In the latency state, an entity is responsible for managing which entity is its parent entity.
        // However the list of child entities is up to the children to update.
        // Resetting the latency state to game state only resets the parent entity, the list of child entities
        // may not match that of the game state, unless every child also gets reset separately.

        [System.NonSerialized] public EntityData parentEntityData;
        /// <summary>
        /// <para>The order of this list may not match that of <see cref="EntityData.childEntities"/>, even
        /// when the latency state otherwise matches the game state.</para>
        /// </summary>
        [System.NonSerialized] public EntityData[] childEntities = new EntityData[ArrList.MinCapacity];
        [System.NonSerialized] public int childEntitiesCount = 0;

        internal DataDictionary runtimeEventListeners = new DataDictionary();

        public void SendDestroyEntityIA(bool keepChildEntitiesAlive = false) => entitySystem.SendDestroyEntityIA(entityData, keepChildEntitiesAlive);

        public void ApplyEntityData() => logic.ApplyEntityData(this);

        public void TakeControlOfTransformSync(EntityTransformController controller) => logic.TakeControlOfTransformSync(this, controller);

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfTransformSync(
            EntityTransformController releasingController,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float interpolationDuration = TransformChangeInterpolationDuration)
        {
            // The amount of wasted SetProgramVariable calls eww.
            // Udon and its stupid eternity it takes to instantiate scripts, causing me to do this.
            // I am disgusted.
            logic.GiveBackControlOfTransformSync(this, releasingController, position, rotation, scale, interpolationDuration);
        }

        /// <summary>
        /// <para>World position stays.</para>
        /// <para>Cancels any ongoing interpolation done by the <see cref="InterpolationManager"/> for this
        /// entity.</para>
        /// </summary>
        /// <param name="parentEntity"></param>
        /// <param name="doInterpolateRemotely"></param>
        public void SendSetParentEntityIA(Entity parentEntity, bool doInterpolateRemotely) => logic.SendSetParentEntityIA(this, parentEntity, doInterpolateRemotely);

        public void SetParentEntityInLS(
            EntityData parentEntityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool doInterpolate)
        {
            logic.SetParentEntityInLS(this, parentEntityData, position, rotation, scale, doInterpolate);
        }

        /// <summary>
        /// <para>World position stays.</para>
        /// <para>Cancels any ongoing interpolation done by the <see cref="InterpolationManager"/> for this
        /// entity.</para>
        /// </summary>
        /// <param name="parentEntity"></param>
        public void SetParentEntityInLS(Entity parentEntity) => logic.SetParentEntityInLS(this, parentEntity);

        /// <summary>
        /// <para>Ignores/Prevents double registrations.</para>
        /// <para>It is recommended for systems to provide a <see langword="const"/> such as
        /// <c>OnFooEventName = "OnFoo";</c> (where <c>Foo</c> is the custom event name).</para>
        /// <para>Then the actual event handler function on the listener must be
        /// <c>public void OnFoo()</c>.</para>
        /// <para>For events registered for the entire lifetime of an entity, register events inside of
        /// <see cref="EntityExtension.AssociateWithExtensionData"/>.</para>
        /// </summary>
        /// <param name="listener">Must not be <see langword="null"/>.</param>
        /// <param name="eventName">Must not be <see langword="null"/>.</param>
        public void RegisterForRuntimeEvent(UdonSharpBehaviour listener, string eventName) => logic.RegisterForRuntimeEvent(this, listener, eventName);

        /// <summary>
        /// <para>Allows trying to deregister a not actually registered listener.</para>
        /// <para>It is recommended for systems to provide a <see langword="const"/> such as
        /// <c>OnFooEventName = "OnFoo";</c> (where <c>Foo</c> is the custom event name).</para>
        /// <para>Then the actual event handler function on the listener must be
        /// <c>public void OnFoo()</c>.</para>
        /// </summary>
        /// <param name="listener">Must not be <see langword="null"/>.</param>
        /// <param name="eventName">Must not be <see langword="null"/>.</param>
        public void DeregisterFromRuntimeEvent(UdonSharpBehaviour listener, string eventName) => logic.DeregisterFromRuntimeEvent(this, listener, eventName);

        /// <summary>
        /// <para>Raise an event to all listeners registered through
        /// <see cref="RegisterForRuntimeEvent(UdonSharpBehaviour, string)"/> for this entity.</para>
        /// <para>It is recommended for systems to provide a <see langword="const"/> such as
        /// <c>OnFooEventName = "OnFoo";</c> (where <c>Foo</c> is the custom event name).</para>
        /// <para>Then the actual event handler function on the listener must be
        /// <c>public void OnFoo()</c>.</para>
        /// <para>Can be called recursively, though the events will not be raised recursively. Rather they
        /// will be enqueued to be raised after the current event. This applies to all events raised for/on
        /// any entity, only 1 of them will be dispatching, all others will be enqueued.</para>
        /// <para>You can thank Udon for being horrendous to use with recursion.</para>
        /// </summary>
        /// <param name="eventName">Must not be <see langword="null"/>.</param>
        public void RaiseRuntimeEvent(string eventName) => logic.RaiseRuntimeEvent(this, eventName);

        // The rest of the logic below is kept in this file, even though that makes instantiation slower,
        // because the increased runtime cost of added Get/SetProgramVariable calls at runtime are not worth it.

        private void EnqueueTransformChangeIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  EnqueueTransformChangeIA");
#endif
            if (transformChangeIAIsQueued)
                return;
            transformChangeIAIsQueued = true;
            float timeUntilNextMovementIA = TimeBetweenTransformChangeIAs - (Time.time - timeAtLastTransformChangeIA);
            if (timeUntilNextMovementIA <= 0f)
                SendCustomEventDelayedFrames(nameof(SendTransformChangeIA), 1);
            else
                SendCustomEventDelayedSeconds(nameof(SendTransformChangeIA), timeUntilNextMovementIA);
        }

        public void FlagForPositionChange(bool flagForDiscontinuity = false)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  FlagForPositionChange");
#endif
            if (noTransformSync)
                return;
            if (flagForDiscontinuity)
                flaggedForDiscontinuousPositionChange = true;
            flaggedForPositionChange = true;
            EnqueueTransformChangeIA();
        }

        public void FlagForRotationChange(bool flagForDiscontinuity = false)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  FlagForRotationChange");
#endif
            if (noTransformSync)
                return;
            if (flagForDiscontinuity)
                flaggedForDiscontinuousRotationChange = true;
            flaggedForRotationChange = true;
            EnqueueTransformChangeIA();
        }

        public void FlagForPositionAndRotationChange(bool flagForDiscontinuity = false)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  FlagForPositionAndRotationChange");
#endif
            FlagForPositionChange(flagForDiscontinuity);
            FlagForRotationChange(flagForDiscontinuity);
        }

        public void FlagForScaleChange(bool flagForDiscontinuity = false)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  FlagForScaleChange");
#endif
            if (noTransformSync)
                return;
            if (flagForDiscontinuity)
                flaggedForDiscontinuousScaleChange = true;
            if (flaggedForScaleChange)
                return;
            flaggedForScaleChange = true;
            EnqueueTransformChangeIA();
        }

        private void ResetTransformChangeFlags()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  ResetTransformChangeFlags");
#endif
            transformChangeIAIsQueued = false;
            flaggedForPositionChange = false;
            flaggedForDiscontinuousPositionChange = false;
            flaggedForRotationChange = false;
            flaggedForDiscontinuousRotationChange = false;
            flaggedForScaleChange = false;
            flaggedForDiscontinuousScaleChange = false;
        }

        /// <summary>
        /// <para>Input action handler is here:
        /// <see cref="Internal.EntityDataLogic.OnTransformChangeIA"/>.</para>
        /// </summary>
        public void SendTransformChangeIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  SendTransformChangeIA");
#endif
            entitySystem.WriteEntityDataRef(entityData);
            lockstep.WriteFlags(
                flaggedForPositionChange, flaggedForDiscontinuousPositionChange,
                flaggedForRotationChange, flaggedForDiscontinuousRotationChange,
                flaggedForScaleChange, flaggedForDiscontinuousScaleChange);

            int startPosition = lockstep.WriteStreamPosition;

            Transform t = this.transform;
            if (flaggedForPositionChange)
                lockstep.WriteVector3(t.localPosition);
            if (flaggedForRotationChange)
                lockstep.WriteQuaternion(t.localRotation);
            if (flaggedForScaleChange)
                lockstep.WriteVector3(t.localScale);
            ResetTransformChangeFlags();

            if (lockstep.WriteStreamPosition == startPosition)
            {
                lockstep.ResetWriteStream();
                return;
            }

            timeAtLastTransformChangeIA = Time.time;
            entityData.RegisterLatencyHiddenUniqueId(((Internal.EntitySystem)entitySystem).SendTransformChangeIA());
        }
    }

    public static class EntityExtensions
    {
        public static int IndexOfExtension(this Entity entity, string extensionDataClassName, int startIndex = 0)
            => System.Array.IndexOf(entity.prototype.ExtensionDataClassNames, extensionDataClassName, startIndex);

        public static EntityExtension GetExtensionDynamic(this Entity entity, string extensionDataClassName, int startIndex = 0)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  GetExtensionDynamic");
#endif
            int extensionIndex = System.Array.IndexOf(entity.prototype.ExtensionDataClassNames, extensionDataClassName, startIndex);
            return extensionIndex < 0 ? null : entity.extensions[extensionIndex];
        }

        public static T GetExtension<T>(this Entity entity, string extensionDataClassName, int startIndex = 0)
            where T : EntityExtension
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  GetExtension");
#endif
            // Same as GetExtensionDynamic.
            int extensionIndex = System.Array.IndexOf(entity.prototype.ExtensionDataClassNames, extensionDataClassName, startIndex);
            return (T)(extensionIndex < 0 ? null : entity.extensions[extensionIndex]);
        }
    }
}
