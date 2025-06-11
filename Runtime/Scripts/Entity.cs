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

        [System.NonSerialized] public bool noPositionSync;
        [System.NonSerialized] public bool noRotationSync;
        [System.NonSerialized] public bool noScaleSync;
        [System.NonSerialized] public UdonSharpBehaviour positionSyncController;
        [System.NonSerialized] public UdonSharpBehaviour rotationSyncController;
        [System.NonSerialized] public UdonSharpBehaviour scaleSyncController;

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

        public void OnInstantiate(
            LockstepAPI lockstep,
            EntitySystem entitySystem,
            WannaBeClassesManager wannaBeClasses,
            EntityPrototype prototype,
            bool isDefaultInstance)
        {
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  SendDestroyEntityIA");
#endif
            entitySystem.SendDestroyEntityIA(entityData);
        }

        public void AssociateWithEntityData(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  AssociateWithEntityData");
            if (this.entityData != null)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromEntityData an Entity multiple times.");
#endif
            entityData.SetEntity(this);
            ApplyEntityDataWithoutExtensions();
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
                allExtensionData[i].SetExtension(extensions[i]);
            for (int i = 0; i < length; i++)
                extensions[i].AssociateWithExtensionData();
            entityData.OnAssociatedWithEntity();
        }

        private void ApplyEntityDataWithoutExtensions()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityDataWithoutExtensions");
#endif
            Transform t = this.transform;
            var interpolation = entityData.interpolation;
            if (!noPositionSync && !entityData.NoPositionSync)
            {
                interpolation.CancelPositionInterpolation(t);
                t.position = entityData.position;
            }
            if (!noRotationSync && !entityData.NoRotationSync)
            {
                interpolation.CancelRotationInterpolation(t);
                t.rotation = entityData.rotation;
            }
            if (!noScaleSync && !entityData.NoScaleSync)
            {
                interpolation.CancelLocalScaleInterpolation(t);
                t.localScale = entityData.scale;
            }
            // TODO: what to do about hidden?
            // TODO: handle parent entity
            // TODO: handle child entities
        }

        public void ApplyEntityData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityData");
#endif
            ApplyEntityDataWithoutExtensions();
            foreach (EntityExtension extension in extensions)
                extension.ApplyExtensionData();
        }

        public void DisassociateFromEntityDataAndReset(Entity defaultEntityInst)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  DisassociateFromEntityDataAndReset");
#endif
            // TODO: this probably needs to do more
            var defaultExtensions = defaultEntityInst.extensions;
            for (int i = 0; i < extensions.Length; i++)
            {
                EntityExtension extension = extensions[i];
                extension.DisassociateFromExtensionDataAndReset(defaultExtensions[i]);
                extension.extensionData = null;
            }
            entityData = null;
        }

        public void OnDestroyEntity()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  OnDestroyEntity");
#endif
        }

        public void TakeControlOfPositionSync(UdonSharpBehaviour controller)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  TakeControlOfPositionSync");
#endif
            if (!noPositionSync)
            {
                noPositionSync = true;
                positionSyncController = controller;
                return;
            }
            if (controller == positionSyncController)
                return;
            UdonSharpBehaviour prevController = positionSyncController;
            positionSyncController = controller;
            positionSyncController.SetProgramVariable(EntityData.ControlledEntityDataField, entityData);
            positionSyncController.SendCustomEvent(EntityData.OnLatencyPositionSyncControlLostEvent);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfPositionSync(UdonSharpBehaviour releasingController, Vector3 position)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  GiveBackControlOfPositionSync");
#endif
            GiveBackControlOfPositionSync(releasingController, position, TransformChangeInterpolationDuration);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfPositionSync(UdonSharpBehaviour releasingController, Vector3 position, float interpolationDuration)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  GiveBackControlOfPositionSync");
#endif
            if (!noPositionSync && (releasingController != null || positionSyncController != releasingController))
                return;
            noPositionSync = false;
            UdonSharpBehaviour prevController = positionSyncController;
            positionSyncController = null;
            if (releasingController == null)
            {
                prevController.SetProgramVariable(EntityData.ControlledEntityDataField, entityData);
                prevController.SendCustomEvent(EntityData.OnLatencyPositionSyncControlLostEvent);
            }
            if (transform.position == position)
                entityData.interpolation.CancelPositionInterpolation(this.transform);
            else
                entityData.interpolation.InterpolateWorldPosition(this.transform, position, interpolationDuration);
        }

        public void TakeControlOfRotationSync(UdonSharpBehaviour controller)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  TakeControlOfRotationSync");
#endif
            if (!noRotationSync)
            {
                noRotationSync = true;
                rotationSyncController = controller;
                return;
            }
            if (controller == rotationSyncController)
                return;
            UdonSharpBehaviour prevController = rotationSyncController;
            rotationSyncController = controller;
            rotationSyncController.SetProgramVariable(EntityData.ControlledEntityDataField, entityData);
            rotationSyncController.SendCustomEvent(EntityData.OnLatencyRotationSyncControlLostEvent);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfRotationSync(UdonSharpBehaviour releasingController, Quaternion rotation)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  GiveBackControlOfRotationSync");
#endif
            GiveBackControlOfRotationSync(releasingController, rotation, TransformChangeInterpolationDuration);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfRotationSync(UdonSharpBehaviour releasingController, Quaternion rotation, float interpolationDuration)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  GiveBackControlOfRotationSync");
#endif
            if (!noRotationSync || (releasingController != null && rotationSyncController != releasingController))
                return;
            noRotationSync = false;
            UdonSharpBehaviour prevController = rotationSyncController;
            rotationSyncController = null;
            if (releasingController == null)
            {
                prevController.SetProgramVariable(EntityData.ControlledEntityDataField, entityData);
                prevController.SendCustomEvent(EntityData.OnLatencyRotationSyncControlLostEvent);
            }
            if (transform.rotation == rotation)
                entityData.interpolation.CancelRotationInterpolation(this.transform);
            else
                entityData.interpolation.InterpolateWorldRotation(this.transform, rotation, interpolationDuration);
        }

        public void TakeControlOfScaleSync(UdonSharpBehaviour controller)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  TakeControlOfScaleSync");
#endif
            if (!noScaleSync)
            {
                noScaleSync = true;
                scaleSyncController = controller;
                return;
            }
            if (controller == scaleSyncController)
                return;
            UdonSharpBehaviour prevController = scaleSyncController;
            scaleSyncController = controller;
            scaleSyncController.SetProgramVariable(EntityData.ControlledEntityDataField, entityData);
            scaleSyncController.SendCustomEvent(EntityData.OnLatencyScaleSyncControlLostEvent);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfScaleSync(UdonSharpBehaviour releasingController, Vector3 scale)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  GiveBackControlOfScaleSync");
#endif
            GiveBackControlOfScaleSync(releasingController, scale, TransformChangeInterpolationDuration);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfScaleSync(UdonSharpBehaviour releasingController, Vector3 scale, float interpolationDuration)
        {
#if EntitySystemDebug
            Debug.Log("[EntitySystemDebug] Entity  GiveBackControlOfScaleSync");
#endif
            if (!noScaleSync || (releasingController != null && scaleSyncController != releasingController))
                return;
            noScaleSync = false;
            UdonSharpBehaviour prevController = scaleSyncController;
            scaleSyncController = null;
            if (releasingController == null)
            {
                prevController.SetProgramVariable(EntityData.ControlledEntityDataField, entityData);
                prevController.SendCustomEvent(EntityData.OnLatencyScaleSyncControlLostEvent);
            }
            if (transform.localScale == scale)
                entityData.interpolation.CancelLocalScaleInterpolation(this.transform);
            else
                entityData.interpolation.InterpolateLocalScale(this.transform, scale, interpolationDuration);
        }

        private void EnqueueTransformChangeIA()
        {
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  FlagForPositionChange");
#endif
            if (noPositionSync)
                return;
            if (flagForDiscontinuity)
                flaggedForDiscontinuousPositionChange = true;
            flaggedForPositionChange = true;
            EnqueueTransformChangeIA();
        }

        public void FlagForRotationChange(bool flagForDiscontinuity = false)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  FlagForRotationChange");
#endif
            if (noRotationSync)
                return;
            if (flagForDiscontinuity)
                flaggedForDiscontinuousRotationChange = true;
            flaggedForRotationChange = true;
            EnqueueTransformChangeIA();
        }

        public void FlagForPositionAndRotationChange(bool flagForDiscontinuity = false)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  FlagForPositionAndRotationChange");
#endif
            FlagForPositionChange(flagForDiscontinuity);
            FlagForRotationChange(flagForDiscontinuity);
        }

        public void FlagForScaleChange(bool flagForDiscontinuity = false)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  FlagForScaleChange");
#endif
            if (noScaleSync)
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
#if EntitySystemDebug
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
#if EntitySystemDebug
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
            entitySystem.SendTransformChangeIA();
        }
    }
}
