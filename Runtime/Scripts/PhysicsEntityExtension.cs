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

        [System.NonSerialized] public Rigidbody rb;

        private VRCPlayerApi localPlayer;
        private uint localPlayerId;
        private bool updateLoopIsRunning;
        private bool updateLoopShouldBeRunning;

        public override void OnInstantiate()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  OnInstantiate");
#endif
            rb = GetComponent<Rigidbody>();
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
        }

        public override void AssociateWithExtensionData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  AssociateWithExtensionData");
#endif
            data = (PhysicsEntityExtensionData)extensionData;
            data.ext = this;
            this.transform.SetPositionAndRotation(data.position, data.rotation);
            ApplyExtensionData();
            UpdateUpdateLoopRunningState();
        }

        public override void ApplyExtensionData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  ApplyExtensionData");
#endif
            ApplyDataToRigidbody();
        }

        public void ApplyDataToRigidbody()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  ApplyDataToRigidbody - data.velocity: {data.velocity}");
#endif
            rb.Move(data.position, data.rotation);
            rb.velocity = data.velocity;
            rb.angularVelocity = data.angularVelocity;
        }

        public override void DisassociateFromExtensionDataAndReset(EntityExtension defaultExtension)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  DisassociateFromExtensionDataAndReset");
#endif
            data.ext = null;
            data = null;
            updateLoopShouldBeRunning = false;
        }

        public void UpdateUpdateLoopRunningState()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtension  UpdateUpdateLoopRunningState");
#endif
            updateLoopShouldBeRunning = data.responsiblePlayerId == localPlayerId;
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
            data.SendRigidbodyUpdateIA();
            SendCustomEventDelayedSeconds(nameof(UpdateLoop), 1f);
        }
    }
}
