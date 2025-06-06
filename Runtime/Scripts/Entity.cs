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
            if (!entityData.NoPositionSync)
            {
                interpolation.CancelPositionInterpolation(t);
                t.position = entityData.position;
            }
            if (!entityData.NoRotationSync)
            {
                interpolation.CancelRotationInterpolation(t);
                t.rotation = entityData.rotation;
            }
            if (!entityData.NoScaleSync)
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
            if (entityData.NoPositionSync)
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
            if (entityData.NoRotationSync)
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
            if (entityData.NoScaleSync)
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
