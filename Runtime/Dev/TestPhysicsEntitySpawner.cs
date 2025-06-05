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
#if EntitySystemDebug
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
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  SpawnAndThrowEntity");
#endif
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
        }

        [HideInInspector][SerializeField] private uint onCreatePhysicsEntityIAId;
        [LockstepInputAction(nameof(onCreatePhysicsEntityIAId))]
        public void OnCreatePhysicsEntityIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  OnCreatePhysicsEntityIA");
#endif
            Vector3 velocity = lockstep.ReadVector3();
            EntityData entityData = entitySystem.ReadEntityInCustomCreateEntityIA();

            PhysicsEntityExtensionData extensionData = GetExtensionData(entityData);
            extensionData.velocity = velocity;
            extensionData.responsiblePlayerId = lockstep.SendingPlayerId;

            entitySystem.RaiseOnEntityCreatedInCustomCreateEntityIA(entityData);
        }

        private PhysicsEntityExtensionData GetExtensionData(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] TestPhysicsEntitySpawner  GetExtensionData");
#endif
            // TODO: have a better way to get specific extension data from entities and or entity data.
            int extensionIndex = System.Array.IndexOf(entityData.entityPrototype.ExtensionDataClassNames, nameof(PhysicsEntityExtensionData));
            return (PhysicsEntityExtensionData)entityData.allExtensionData[extensionIndex];
        }
    }
}
