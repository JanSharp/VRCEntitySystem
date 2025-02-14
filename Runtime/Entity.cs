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
        private const float TimeBetweenTransformChangeIAs = 0.2f;
        private DataDictionary latencyHiddenUniqueIds = new DataDictionary();

        public void InitFromEntityData(EntityData entityData)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  InitFromEntityData");
            if (this.entityData != null)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromEntityData an Entity multiple times.");
            #endif
            entityData.SetEntity(this);
            ApplyEntityDataWithoutExtension();
            lockstep = entityData.lockstep;
            entitySystem = entityData.entitySystem;
            prototype = entityData.entityPrototype;
            this.entityData = entityData;
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
            {
                EntityExtension extension = extensions[i];
                extension.Setup(i, lockstep, entitySystem, this);
                EntityExtensionData extensionData = allExtensionData[i];
                if (extensionData != null)
                {
                    extensionData.SetExtension(extension);
                    extension.InitFromExtensionData();
                }
                else
                {
                    extensionData = (EntityExtensionData)wannaBeClasses.NewDynamic(prototype.ExtensionDataClassNames[i]);
                    allExtensionData[i] = extensionData;
                    extensionData.SetExtension(extension);
                    extensionData.InitFromExtension();
                }
            }
        }

        private void ApplyEntityDataWithoutExtension()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityDataWithoutExtension");
            #endif
            Transform t = this.transform;
            t.position = entityData.position;
            t.rotation = entityData.rotation;
            t.localScale = entityData.scale;
            // TODO: what to do about hidden?
            // TODO: handle parent entity
            // TODO: handle child entities
        }

        public void ApplyEntityData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  ApplyEntityData");
            #endif
            ApplyEntityDataWithoutExtension();
            foreach (EntityExtension extension in extensions)
                extension.ApplyExtensionData();
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
            if (entityData.transformState != EntityTransformState.Synced)
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
            if (entityData.transformState != EntityTransformState.Synced)
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
            if (entityData.transformState != EntityTransformState.Synced)
                return;
            if (flagForDiscontinuity)
            {
                flaggedForDiscontinuousPositionChange = true;
                flaggedForDiscontinuousRotationChange = true;
            }
            flaggedForPositionChange = true;
            flaggedForRotationChange = true;
            EnqueueTransformChangeIA();
        }

        public void FlagForScaleChange(bool flagForDiscontinuity = false)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  FlagForScaleChange");
            #endif
            if (entityData.transformState != EntityTransformState.Synced)
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

        public void SendTransformChangeIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  SendTransformChangeIA");
            #endif
            if (entityData.transformState != EntityTransformState.Synced)
            {
                ResetTransformChangeFlags();
                return;
            }
            timeAtLastTransformChangeIA = Time.time;

            lockstep.WriteSmallUInt(entityData.id);
            lockstep.WriteFlags(
                flaggedForPositionChange, flaggedForDiscontinuousPositionChange,
                flaggedForRotationChange, flaggedForDiscontinuousRotationChange,
                flaggedForScaleChange, flaggedForDiscontinuousScaleChange);

            if (flaggedForPositionChange)
                lockstep.WriteVector3(this.transform.position);
            if (flaggedForRotationChange)
                lockstep.WriteQuaternion(this.transform.rotation);
            if (flaggedForScaleChange)
                lockstep.WriteVector3(this.transform.localScale);

            ResetTransformChangeFlags();
            ulong uniqueId = entitySystem.SendTransformChangeIA();
            latencyHiddenUniqueIds.Add(uniqueId, true);
        }

        public void OnTransformChangeIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  OnTransformChangeIA");
            #endif
            lockstep.ReadFlags(
                out bool positionChange, out bool discontinuousPositionChange,
                out bool rotationChange, out bool discontinuousRotationChange,
                out bool scaleChange, out bool discontinuousScaleChange);

            if (positionChange)
                entityData.position = lockstep.ReadVector3();
            if (rotationChange)
                entityData.rotation = lockstep.ReadQuaternion();
            if (scaleChange)
                entityData.scale = lockstep.ReadVector3();

            if (latencyHiddenUniqueIds.Remove(lockstep.SendingUniqueId))
                return;
            if (entityData.transformState != EntityTransformState.Synced)
                return;

            if (positionChange)
            {
                // TODO: Interpolate and respect discontinuity.
                this.transform.position = entityData.position;
            }
            if (rotationChange)
            {
                // TODO: Interpolate and respect discontinuity.
                this.transform.rotation = entityData.rotation;
            }
            if (scaleChange)
            {
                // TODO: Interpolate and respect discontinuity.
                this.transform.localScale = entityData.scale;
            }
        }
    }
}
