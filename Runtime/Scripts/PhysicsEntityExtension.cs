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

        private VRCPlayerApi localPlayer;
        private uint localPlayerId;
        private int interpolationCounter;
        private bool updateLoopIsRunning;
        private bool updateLoopShouldBeRunning;

        public override void OnInstantiate()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  OnInstantiate");
#endif
            rb = GetComponent<Rigidbody>();
            isSleeping = rb.isKinematic;
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
        }

        public override void DisassociateFromExtensionDataAndReset(EntityExtension defaultExtension)
        {
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  AssociateWithExtensionData");
#endif
            data = (PhysicsEntityExtensionData)extensionData;
            data.ext = this;
            // Prevent the entity being at the wrong (prefab default) position for a frame or two.
            if (!data.isSleeping)
                this.transform.SetPositionAndRotation(data.position, data.rotation);
            ApplyExtensionData(); // This calls rb.Move too, hopefully that'll make the rb happier.
            SendCustomEventDelayedSeconds(nameof(UpdateUpdateLoopRunningState), PhysicsSyncInterval);
        }

        public override void ApplyExtensionData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  ApplyExtensionData");
#endif
            if (!data.isSleeping)
            {
                rb.isKinematic = false; // Move only works on non kinematic rigidbodies.
                rb.Move(data.position, data.rotation); // Teleport instead of interpolation.
            }
            ApplyDataToRigidbody();
        }

        public void ApplyDataToRigidbody()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  ApplyDataToRigidbody - data.velocity: {data.velocity}");
#endif
            if (data.isSleeping)
            {
                if (isSleeping)
                    return;
                isSleeping = true;
                rb.isKinematic = true;
                SendCustomEventDelayedSeconds(nameof(UpdateUpdateLoopRunningState), PhysicsSyncInterval);
                return;
            }

            if (isSleeping)
            {
                isSleeping = false;
                rb.isKinematic = false;
                SendCustomEventDelayedSeconds(nameof(UpdateUpdateLoopRunningState), PhysicsSyncInterval);
            }

            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  ApplyDataToRigidbody (inner) - posDiff: {Vector3.Distance(rb.position, data.position)}, rotDiff: {Vector3.Dot(rb.rotation * Vector3.forward, data.rotation * Vector3.forward)}");

            if (interpolationCounter == 0
                && (Vector3.Distance(rb.position, data.position) < PositionalOffsetTolerance
                    || Vector3.Dot(rb.rotation * Vector3.forward, data.rotation * Vector3.forward) > RotationalOffsetTolerance))
            {
                // It is close enough, do not change the position and rotation.
                // rb.Move(data.position, data.rotation);
                rb.velocity = data.velocity;
                rb.angularVelocity = data.angularVelocity;
                return;
            }
            InterpolateToDataPositionAndRotation(data.position, data.rotation);
        }

        private void InterpolateToDataPositionAndRotation(Vector3 position, Quaternion rotation)
        {
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  OnInterpolationFinished");
#endif
            if ((--interpolationCounter) != 0 || isSleeping)
                return;
            rb.isKinematic = false;
            rb.velocity = data.velocity;
            rb.angularVelocity = data.angularVelocity;
        }

        public void UpdateUpdateLoopRunningState()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  UpdateUpdateLoopRunningState");
#endif
            updateLoopShouldBeRunning = !isSleeping && data.responsiblePlayerId == localPlayerId;
            StartUpdateLoop();
        }

        private void StartUpdateLoop()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  StartUpdateLoop - updateLoopShouldBeRunning: {updateLoopShouldBeRunning}");
#endif
            if (updateLoopIsRunning || !updateLoopShouldBeRunning)
                return;
            updateLoopIsRunning = true;
            UpdateLoop();
        }

        public void UpdateLoop()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  UpdateLoop");
#endif
            if (!updateLoopShouldBeRunning)
            {
                updateLoopIsRunning = false;
                return;
            }
            if (rb.IsSleeping())
            {
                data.SendRigidbodySleepIA();
                updateLoopIsRunning = false;
                return;
            }
            data.SendRigidbodyUpdateIA();
            SendCustomEventDelayedSeconds(nameof(UpdateLoop), PhysicsSyncInterval);
        }
    }
}
