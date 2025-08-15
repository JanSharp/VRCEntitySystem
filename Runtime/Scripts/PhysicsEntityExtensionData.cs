using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

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

        [System.NonSerialized] public PhysicsEntityExtension ext;

        [System.NonSerialized] public uint responsiblePlayerId;
        [System.NonSerialized] public bool isSleeping = true;
        [System.NonSerialized] public Vector3 velocity;
        [System.NonSerialized] public Vector3 angularVelocity;

        private uint localPlayerId;

        public override void InitFromDefault(EntityExtension entityExtension)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  InitFromDefault");
#endif
            PhysicsEntityExtension ext = (PhysicsEntityExtension)entityExtension;
            if (!ext.isSleeping)
                WakeUp();
            localPlayerId = (uint)Networking.LocalPlayer.playerId;
        }

        public override void InitFromPreInstantiated(EntityExtension entityExtension)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  InitFromPreInstantiated");
#endif
            InitFromDefault(entityExtension);
        }

        public override void OnEntityExtensionDataCreated()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnEntityExtensionDataCreated");
#endif
            if (responsiblePlayerId == 0u)
                responsiblePlayerId = lockstep.MasterPlayerId;
            manager.RegisterPhysicsExtensionData(this);
        }

        public override void OnAssociatedWithExtension()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnAssociatedWithExtension");
#endif
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
            if (ext.responsiblePlayerId != localPlayerId)
                ext.SetResponsiblePlayerId(localPlayerId);
            if (ext.isSleeping)
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
                Debug.LogError("[EntitySystem] Impossible, attempt to SendRigidbodyUpdateIA on a physics "
                    + "entity which is asleep or the local player is not the responsible player.");
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

        public override void Serialize(bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  Serialize");
#endif
            if (!isExport)
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
            if (!isImport)
            {
                responsiblePlayerId = lockstep.ReadSmallUInt();
                manager.RegisterPhysicsExtensionData(this);
            }
            lockstep.ReadFlags(out isSleeping);
            if (isSleeping)
                ResetGameStateDueToSleep();
            else
            {
                velocity = lockstep.ReadVector3();
                angularVelocity = lockstep.ReadVector3();
                entityData.SetTransformSyncControllerDueToDeserialization(transformController);
            }
        }
    }
}
