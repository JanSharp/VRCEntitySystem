using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TestPhysicsEntitySpawner : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private EntitySystem entitySystem;
        [HideInInspector][SerializeField][SingletonReference] private LockstepAPI lockstep;

        public string entityPrototypeName;

        private VRCPlayerApi localPlayer;
        private uint localPlayerId;

        private void Start()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  Start");
#endif
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
                SpawnAndThrowEntity();
        }

        private void SpawnAndThrowEntity()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  SpawnAndThrowEntity");
#endif
            if (!lockstep.IsInitialized)
                return;
            if (!entitySystem.TryGetEntityPrototype(entityPrototypeName, out EntityPrototype prototype))
                return;
            var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 velocity = head.rotation * Vector3.forward * 10f;
            lockstep.WriteVector3(velocity);
            EntityData entityData = entitySystem.SendCustomCreateEntityIA(
                onCreatePhysicsEntityIAId,
                prototype.Id,
                head.position + head.rotation * Vector3.forward,
                head.rotation);

            PhysicsEntityExtensionData extensionData = GetExtensionData(entityData);
            extensionData.velocity = velocity;
            extensionData.responsiblePlayerId = localPlayerId;
            EnsureIsAwake(extensionData);
        }

        [HideInInspector][SerializeField] private uint onCreatePhysicsEntityIAId;
        [LockstepInputAction(nameof(onCreatePhysicsEntityIAId))]
        public void OnCreatePhysicsEntityIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  OnCreatePhysicsEntityIA");
#endif
            Vector3 velocity = lockstep.ReadVector3();
            EntityData entityData = entitySystem.ReadEntityInCustomCreateEntityIA(onEntityCreatedGetsRaisedLater: true);

            if (lockstep.SendingPlayerId != localPlayerId) // The sending local player already performed this initialization.
            {
                PhysicsEntityExtensionData extensionData = GetExtensionData(entityData);
                extensionData.velocity = velocity;
                extensionData.responsiblePlayerId = lockstep.SendingPlayerId;
                EnsureIsAwake(extensionData);
            }

            entitySystem.RaiseOnEntityCreatedInCustomCreateEntityIA(entityData);
        }

        private void EnsureIsAwake(PhysicsEntityExtensionData extensionData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  EnsureIsAwake");
#endif
            if (!extensionData.isSleeping)
                return;
            extensionData.WakeUp();
        }

        private PhysicsEntityExtensionData GetExtensionData(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  GetExtensionData");
#endif
            return entityData.GetExtensionData<PhysicsEntityExtensionData>(nameof(PhysicsEntityExtensionData));
        }
    }
}
