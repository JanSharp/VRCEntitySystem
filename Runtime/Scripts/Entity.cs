using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Entity : UdonSharpBehaviour
    {
        [System.NonSerialized] public LockstepAPI lockstep;
        [System.NonSerialized] public EntitySystem entitySystem;
        [System.NonSerialized] public WannaBeClassesManager wannaBeClasses;
        [System.NonSerialized] public EntityPrototype prototype;
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public int instanceIndex;

        [System.NonSerialized] public bool noTransformSync;
        [System.NonSerialized] public EntityTransformController transformSyncController;

        [HideInInspector] public EntityExtension[] extensions;

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

        public void OnInstantiate(
            LockstepAPI lockstep,
            EntitySystem entitySystem,
            WannaBeClassesManager wannaBeClasses,
            EntityPrototype prototype,
            bool isDefaultInstance)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  OnInstantiate");
#endif
            this.lockstep = lockstep;
            this.entitySystem = entitySystem;
            this.wannaBeClasses = wannaBeClasses;
            this.prototype = prototype;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
                extensions[i].InternalSetup(i, lockstep, entitySystem, this);
            if (isDefaultInstance)
                for (int i = 0; i < length; i++)
                    extensions[i].OnInstantiateDefaultInstance();
            else
                for (int i = 0; i < length; i++)
                    extensions[i].OnInstantiate();
        }

        public void SendDestroyEntityIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  SendDestroyEntityIA");
#endif
            entitySystem.SendDestroyEntityIA(entityData);
        }

        public void AssociateWithEntityData(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  AssociateWithEntityData");
            if (this.entityData != null)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromEntityData an Entity multiple times.");
#endif
            entityData.SetEntity(this);
            ApplyEntityDataWithoutExtensions();
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
                allExtensionData[i].SetEntityAndExtension(this, extensions[i]);
            for (int i = 0; i < length; i++)
                extensions[i].AssociateWithExtensionData();
            entityData.OnAssociatedWithEntity();
        }

        private void ApplyEntityDataWithoutExtensions()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityDataWithoutExtensions");
#endif
            Transform t = this.transform;
            var interpolation = entityData.interpolation;
            if (!noTransformSync && !entityData.noTransformSync)
            {
                interpolation.CancelPositionInterpolation(t);
                interpolation.CancelRotationInterpolation(t);
                interpolation.CancelScaleInterpolation(t);
                t.position = entityData.position;
                t.rotation = entityData.rotation;
                t.localScale = entityData.scale;
            }
            // TODO: what to do about hidden?
            // TODO: handle parent entity
            // TODO: handle child entities
        }

        public void ApplyEntityData()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityData");
#endif
            ApplyEntityDataWithoutExtensions();
            foreach (EntityExtension extension in extensions)
                extension.ApplyExtensionData();
        }

        public void DisassociateFromEntityDataAndReset(Entity defaultEntityInst)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  DisassociateFromEntityDataAndReset");
#endif
            // TODO: this probably needs to do more
            var defaultExtensions = defaultEntityInst.extensions;
            for (int i = 0; i < extensions.Length; i++)
            {
                EntityExtension extension = extensions[i];
                extension.DisassociateFromExtensionDataAndReset(defaultExtensions[i]);
                extension.entityData = null;
                extension.extensionData = null;
            }
            entityData = null;
        }

        public void OnDestroyEntity()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] Entity  OnDestroyEntity");
#endif
        }

        public void TakeControlOfTransformSync(EntityTransformController controller)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] Entity  TakeControlOfTransformSync");
#endif
            if (!noTransformSync)
            {
                noTransformSync = true;
                transformSyncController = controller;
                return;
            }
            if (controller == transformSyncController)
                return;
            EntityTransformController prevController = transformSyncController;
            transformSyncController = controller;
            prevController.OnLatencyControlTakenOver(this, controller);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfTransformSync(EntityTransformController releasingController, Vector3 position, Quaternion rotation, Vector3 scale, float interpolationDuration = TransformChangeInterpolationDuration)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] Entity  GiveBackControlOfTransformSync");
#endif
            if (!noTransformSync || (releasingController != null && transformSyncController != releasingController))
                return;
            noTransformSync = false;
            EntityTransformController prevController = transformSyncController;
            transformSyncController = null;
            if (releasingController == null)
                prevController.OnLatencyControlLost(this);

            Transform t = this.transform;

            if (t.position == position)
                entityData.interpolation.CancelPositionInterpolation(t);
            else
                entityData.interpolation.LerpWorldPosition(t, position, interpolationDuration);

            if (t.rotation == rotation)
                entityData.interpolation.CancelRotationInterpolation(t);
            else
                entityData.interpolation.LerpWorldRotation(t, rotation, interpolationDuration);

            if (t.localScale == scale)
                entityData.interpolation.CancelScaleInterpolation(t);
            else
                entityData.interpolation.LerpLocalScale(t, scale, interpolationDuration);
        }

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

            if (flaggedForPositionChange)
                lockstep.WriteVector3(this.transform.position);
            if (flaggedForRotationChange)
                lockstep.WriteQuaternion(this.transform.rotation);
            if (flaggedForScaleChange)
                lockstep.WriteVector3(this.transform.localScale);
            ResetTransformChangeFlags();

            if (lockstep.WriteStreamPosition == startPosition)
            {
                lockstep.ResetWriteStream();
                return;
            }

            timeAtLastTransformChangeIA = Time.time;
            entityData.RegisterLatencyHiddenUniqueId(entitySystem.SendTransformChangeIA());
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
