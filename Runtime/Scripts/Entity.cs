using UdonSharp;
using UnityEngine;

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

        public void SendDestroyEntityIA() => entitySystem.SendDestroyEntityIA(entityData);

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
        /// <para>Input action handler is here: <see cref="EntityData.OnTransformChangeIA"/>.</para>
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
                lockstep.WriteVector3(t.position);
            if (flaggedForRotationChange)
                lockstep.WriteQuaternion(t.rotation);
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
