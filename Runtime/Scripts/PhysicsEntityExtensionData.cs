using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PhysicsEntityExtensionData : EntityExtensionData
    {
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        [HideInInspector][SingletonReference] public PhysicsEntityManager manager;
        [HideInInspector][SingletonReference] public PhysicsEntityTransformController transformController;
        [HideInInspector][SingletonReference] public InterpolationManager interpolation;
        [HideInInspector][SingletonReference] public PlayerDataManager playerDataManager;

        [System.NonSerialized] public PhysicsEntityExtension ext;

        /// <summary>
        /// <para>Can be <c>0u</c>.</para>
        /// </summary>
        [System.NonSerialized] public uint responsiblePlayerId;
        [System.NonSerialized] public bool isSleeping = true;
        [System.NonSerialized] public Vector3 velocity;
        [System.NonSerialized] public Vector3 angularVelocity;

        private uint localPlayerId;

        private void Init()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  Init");
#endif
            localPlayerId = (uint)Networking.LocalPlayer.playerId;
        }

        public override void InitBeforeDeserialization()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  InitBeforeDeserialization");
#endif
            Init();
        }

        public override void InitFromPreInstantiated(EntityExtension entityExtension)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  InitFromPreInstantiated");
#endif
            Init();
            PhysicsEntityExtension ext = (PhysicsEntityExtension)entityExtension;
            if (!ext.isSleeping)
                WakeUp();
        }

        public override void InitFromDefault(EntityExtension entityExtension)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  InitFromDefault");
#endif
            Init();
            responsiblePlayerId = entityData.lastUserPlayerData == null ? 0u : entityData.lastUserPlayerData.PlayerId;
            PhysicsEntityExtension ext = (PhysicsEntityExtension)entityExtension;
            if (!ext.isSleeping)
                WakeUp();
        }

        public override void OnEntityExtensionDataCreated()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnEntityExtensionDataCreated");
#endif
            manager.RegisterPhysicsExtensionData(this);
        }

        public override void OnEntityExtensionDataDestroyed()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnEntityExtensionDataDestroyed");
#endif
            manager.DeregisterPhysicsExtensionData(this);
        }

        public void SetResponsiblePlayerId(uint playerId)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  SetResponsiblePlayerId");
#endif
            manager.DeregisterPhysicsExtensionData(this);
            responsiblePlayerId = playerId;
            manager.RegisterPhysicsExtensionData(this);
        }

        public void SendWakeUpIA(Vector3 velocity, Vector3 angularVelocity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  SendWakeUpIA");
#endif
            entityData.WritePotentiallyUnknownTransformValues();
            lockstep.WriteVector3(velocity);
            lockstep.WriteVector3(angularVelocity);
            ulong uniqueId = SendExtensionDataInputAction(nameof(OnWakeUpIA));
            // Latency hiding.
            if (ext == null)
                return;
            entityData.RegisterLatencyHiddenUniqueId(uniqueId);
            ext.SetResponsiblePlayerId(localPlayerId);
            ext.WakeUp();
        }

        [EntityExtensionDataInputAction]
        public void OnWakeUpIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnWakeUpIA");
#endif
            if (!isSleeping) // Cannot be woken up while already awake.
            {
                entityData.MarkLatencyHiddenUniqueIdAsProcessed();
                return;
            }
            SetResponsiblePlayerId(lockstep.SendingPlayerId);
            entityData.ReadPotentiallyUnknownTransformValues();
            velocity = lockstep.ReadVector3();
            angularVelocity = lockstep.ReadVector3();
            WakeUp();
            if (!entityData.ShouldApplyReceivedIAToLatencyState())
                return;
            ext.SetResponsiblePlayerId(responsiblePlayerId);
            ext.WakeUp();
            ext.rb.velocity = velocity;
            ext.rb.angularVelocity = angularVelocity;
        }

        public void WakeUp()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  WakeUp");
#endif
            if (!isSleeping)
                return;
            isSleeping = false;
            entityData.TakeControlOfTransformSync(transformController);
        }

        public void SendRigidbodyUpdateIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  SendRigidbodyUpdateIA");
#endif
            if (ext.isSleeping || ext.responsiblePlayerId != localPlayerId)
            {
                Debug.LogError($"[EntitySystem] Impossible, attempt to SendRigidbodyUpdateIA on a physics "
                    + $"entity which is asleep or the local player is not the responsible player. "
                    + $"ext.isSleeping: {ext.isSleeping}, ext.responsiblePlayerId: {ext.responsiblePlayerId}, "
                    + $"localPlayerId: {localPlayerId}");
                return;
            }
            Rigidbody rb = ext.rb;
            lockstep.WriteVector3(rb.position);
            lockstep.WriteQuaternion(rb.rotation);
            lockstep.WriteVector3(rb.velocity);
            lockstep.WriteVector3(rb.angularVelocity);
            entityData.RegisterLatencyHiddenUniqueId(SendExtensionDataInputAction(nameof(OnRigidbodyUpdateIA)));
        }

        [EntityExtensionDataInputAction]
        public void OnRigidbodyUpdateIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnRigidbodyUpdateIA");
#endif
            if (isSleeping || lockstep.SendingPlayerId != responsiblePlayerId)
            {
                entityData.MarkLatencyHiddenUniqueIdAsProcessed();
                return;
            }
            entityData.lastKnownTransformStateTick = lockstep.CurrentTick;
            entityData.position = lockstep.ReadVector3();
            entityData.rotation = lockstep.ReadQuaternion();
            velocity = lockstep.ReadVector3();
            angularVelocity = lockstep.ReadVector3();
            if (entityData.ShouldApplyReceivedIAToLatencyState() && ext != null)
                ext.RigidbodyUpdate();
        }

        public void SendGoToSleepIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  SendGoToSleepIA");
#endif
            Transform entityTransform = entity.transform;
            Vector3 position = entityTransform.position;
            Quaternion rotation = entityTransform.rotation;
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            entityData.RegisterLatencyHiddenUniqueId(SendExtensionDataInputAction(nameof(OnGoToSleepIA)));
            // Latency hiding.
            ext.GoToSleep(position, rotation);
        }

        [EntityExtensionDataInputAction]
        public void OnGoToSleepIA()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnGoToSleepIA");
#endif
            if (isSleeping) // Cannot go to sleep while already sleeping.
            {
                entityData.MarkLatencyHiddenUniqueIdAsProcessed();
                return;
            }
            Vector3 position = lockstep.ReadVector3();
            Quaternion rotation = lockstep.ReadQuaternion();
            entityData.position = position;
            entityData.rotation = rotation;
            GoToSleep();
            if (entityData.ShouldApplyReceivedIAToLatencyState() && ext != null)
                ext.GoToSleep(position, rotation);
        }

        public void GoToSleep()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  GoToSleep");
#endif
            if (isSleeping)
                return;
            isSleeping = true;
            ResetGameStateDueToSleep();
            entityData.GiveBackControlOfTransformSync(
                transformController,
                entityData.position,
                entityData.rotation,
                entityData.scale,
                PhysicsEntityExtension.InterpolationDuration);
        }

        private void ResetGameStateDueToSleep()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  ResetGameStateDueToSleep");
#endif
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
        }

        private void ExportResponsiblePlayer()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  ExportResponsiblePlayer");
#endif
            lockstep.WriteSmallUInt(responsiblePlayerId == 0u
                ? 0u
                : playerDataManager.GetCorePlayerDataForPlayerId(responsiblePlayerId).persistentId);
        }

        private void ImportResponsiblePlayer()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  ImportResponsiblePlayer");
#endif
            uint persistentId = lockstep.ReadSmallUInt();
            if (persistentId == 0u)
                return;
            persistentId = playerDataManager.GetPersistentIdFromImportedId(persistentId);
            var playerData = playerDataManager.GetCorePlayerDataForPersistentId(persistentId);
            if (playerData.isOffline)
                return;
            SetResponsiblePlayerId(playerData.playerId);
        }

        public override void Serialize(bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  Serialize");
#endif
            if (isExport)
                ExportResponsiblePlayer();
            else
                lockstep.WriteSmallUInt(responsiblePlayerId);
            lockstep.WriteFlags(isSleeping);
            if (!isSleeping)
            {
                lockstep.WriteVector3(velocity);
                lockstep.WriteVector3(angularVelocity);
            }
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  Deserialize");
#endif
            if (isImport)
                ImportResponsiblePlayer();
            else
            {
                responsiblePlayerId = lockstep.ReadSmallUInt();
                manager.RegisterPhysicsExtensionData(this);
            }
            lockstep.ReadFlags(out isSleeping);
            if (isSleeping)
                ResetGameStateDueToSleep();
            else
            {
                // TODO: This is an issue for imports, there is no responsible player... but there is,
                // it just doesn't start the update loop for whatever reason.
                velocity = lockstep.ReadVector3();
                angularVelocity = lockstep.ReadVector3();
                entityData.SetTransformSyncControllerDueToDeserialization(transformController);
            }
        }
    }
}
