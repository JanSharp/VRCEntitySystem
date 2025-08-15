using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [AssociatedEntityExtensionData(typeof(PhysicsEntityExtensionData))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Entity))]
    [SingletonDependency(typeof(PhysicsEntityManager))]
    [DisallowMultipleComponent]
    public class PhysicsEntityExtension : EntityExtension
    {
        [System.NonSerialized] public PhysicsEntityExtensionData data;

        public const float PhysicsSyncInterval = 1f;
        public const float InterpolationDuration = 0.2f;
        public const float PositionalOffsetTolerance = 0.02f;
        public const float RotationalOffsetTolerance = 0.98f;

        [System.NonSerialized] public Rigidbody rb;

        /// <summary>
        /// <para>Latency state.</para>
        /// </summary>
        [System.NonSerialized] public bool isSleeping;
        [System.NonSerialized] public uint responsiblePlayerId;

        private VRCPlayerApi localPlayer;
        private uint localPlayerId;
        private int interpolationCounter;
        private bool updateLoopIsRunning;
        private bool updateLoopShouldBeRunning;

        public override void OnInstantiate()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  OnInstantiate");
#endif
            rb = GetComponent<Rigidbody>();
            isSleeping = rb.isKinematic;
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
        }

        public override void DisassociateFromExtensionDataAndReset(EntityExtension defaultExtension)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  DisassociateFromExtensionDataAndReset");
#endif
            PhysicsEntityExtension ext = (PhysicsEntityExtension)defaultExtension;
            rb.isKinematic = ext.rb.isKinematic;
            isSleeping = ext.isSleeping;
            data.ext = null;
            data = null;
            updateLoopShouldBeRunning = false;
        }

        public override void AssociateWithExtensionData()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  AssociateWithExtensionData");
#endif
            data = (PhysicsEntityExtensionData)extensionData;
            data.ext = this;
            // Prevent the entity being at the wrong (prefab default) position for a frame or two.
            if (!data.isSleeping)
                this.transform.SetPositionAndRotation(entityData.position, entityData.rotation);
            ApplyExtensionData(); // This calls rb.Move too, hopefully that'll make the rb happier.
            UpdateUpdateLoopRunningState();
        }

        public override void ApplyExtensionData()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  ApplyExtensionData");
#endif
            SetResponsiblePlayerId(data.responsiblePlayerId);

            if (data.isSleeping)
            {
                if (isSleeping)
                    return;
                GoToSleep(entityData.position, entityData.rotation);
                return;
            }

            if (isSleeping)
                WakeUp();

            // After WakeUp() because Move() does nothing on kinematic rigidbodies.
            rb.Move(entityData.position, entityData.rotation); // Teleport instead of interpolation.
            RigidbodyUpdate();
        }

        public void WakeUp()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  WakeUp");
#endif
            if (!isSleeping)
                return;
            isSleeping = false;
            rb.isKinematic = false;
            entity.TakeControlOfTransformSync(data.transformController);
            UpdateUpdateLoopRunningState();
        }

        public void GoToSleep(Vector3 position, Quaternion rotation)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  GoToSleep");
#endif
            if (isSleeping)
                return;
            isSleeping = true;
            rb.isKinematic = true;
            entity.GiveBackControlOfTransformSync(
                data.transformController,
                position,
                rotation,
                entity.transform.localScale,
                InterpolationDuration);
            UpdateUpdateLoopRunningState();
        }

        public void RigidbodyUpdate()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  RigidbodyUpdate");
#endif
            if (isSleeping)
                return;

            Vector3 position = entityData.position;
            Quaternion rotation = entityData.rotation;

            if (interpolationCounter == 0
                && (Vector3.Distance(rb.position, position) < PositionalOffsetTolerance
                    || Vector3.Dot(rb.rotation * Vector3.forward, rotation * Vector3.forward) > RotationalOffsetTolerance))
            {
                // It is close enough, do not change the position and rotation.
                // rb.Move(position, rotation);
                rb.velocity = data.velocity;
                rb.angularVelocity = data.angularVelocity;
                return;
            }

            InterpolateToDataPositionAndRotation(position, rotation);
        }

        private void InterpolateToDataPositionAndRotation(Vector3 position, Quaternion rotation)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  InterpolateToDataPositionAndRotation");
#endif
            InterpolationManager interpolation = data.interpolation;
            interpolation.InterpolateWorldPosition(this.transform, position, InterpolationDuration);
            interpolation.InterpolateWorldRotation(this.transform, rotation, InterpolationDuration, this, nameof(OnInterpolationFinished), null);
            rb.isKinematic = true;
            interpolationCounter++;
        }

        public void OnInterpolationFinished()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  OnInterpolationFinished - interpolationCounter: {interpolationCounter}");
#endif
            if ((--interpolationCounter) != 0 || isSleeping)
                return;
            rb.isKinematic = false;
            rb.velocity = data.velocity;
            rb.angularVelocity = data.angularVelocity;
        }

        public void SetResponsiblePlayerId(uint responsiblePlayerId)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  SetResponsiblePlayerId");
#endif
            this.responsiblePlayerId = responsiblePlayerId;
            UpdateUpdateLoopRunningState();
        }

        public void UpdateUpdateLoopRunningState()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  UpdateUpdateLoopRunningState");
#endif
            updateLoopShouldBeRunning = !isSleeping && responsiblePlayerId == localPlayerId;
            StartUpdateLoop();
        }

        private void StartUpdateLoop()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  StartUpdateLoop - updateLoopShouldBeRunning: {updateLoopShouldBeRunning}");
#endif
            if (updateLoopIsRunning || !updateLoopShouldBeRunning)
                return;
            updateLoopIsRunning = true;
            SendCustomEventDelayedSeconds(nameof(UpdateLoop), PhysicsSyncInterval);
        }

        public void UpdateLoop()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  UpdateLoop");
#endif
            if (!updateLoopShouldBeRunning)
            {
                updateLoopIsRunning = false;
                return;
            }
            if (rb.IsSleeping())
            {
                data.SendGoToSleepIA();
                updateLoopIsRunning = false;
                return;
            }
            data.SendRigidbodyUpdateIA();
            SendCustomEventDelayedSeconds(nameof(UpdateLoop), PhysicsSyncInterval);
        }
    }
}
