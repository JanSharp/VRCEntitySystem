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
        [HideInInspector][SingletonReference] public InterpolationManager interpolation;

        [System.NonSerialized] public PhysicsEntityExtension ext;

        [System.NonSerialized] public uint responsiblePlayerId;
        [System.NonSerialized] public Vector3 position;
        [System.NonSerialized] public Quaternion rotation;
        [System.NonSerialized] public Vector3 velocity;
        [System.NonSerialized] public Vector3 angularVelocity;

        private uint localPlayerId;

        public override void InitFromDefault(EntityExtension entityExtension)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  InitFromDefault");
#endif
            PhysicsEntityExtension ext = (PhysicsEntityExtension)entityExtension;
            position = entityData.position;
            rotation = entityData.rotation;
            entityData.NoPositionSync = true;
            entityData.NoRotationSync = true;
            localPlayerId = (uint)Networking.LocalPlayer.playerId;
        }

        public override void InitFromPreInstantiated(EntityExtension entityExtension)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  InitFromPreInstantiated");
#endif
            InitFromDefault(entityExtension);
        }

        public override void OnEntityExtensionDataCreated()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnEntityExtensionDataCreated");
#endif
            if (responsiblePlayerId == 0u)
                responsiblePlayerId = lockstep.MasterPlayerId;
            manager.RegisterPhysicsExtensionData(this);
        }

        public override void OnAssociatedWithExtension()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnAssociatedWithExtension");
#endif
        }

        public override void OnEntityExtensionDataDestroyed()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnEntityExtensionDataDestroyed");
#endif
            manager.DeregisterPhysicsExtensionData(this);
        }

        public void SetResponsiblePlayerId(uint playerId)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  SetResponsiblePlayerId");
#endif
            manager.DeregisterPhysicsExtensionData(this); // TODO: Also deregister when it gets destroyed.
            responsiblePlayerId = playerId;
            if (ext != null)
                ext.UpdateUpdateLoopRunningState();
            manager.RegisterPhysicsExtensionData(this);
        }

        public void SendRigidbodyUpdateIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  SendRigidbodyUpdateIA");
#endif
            Rigidbody rb = ext.rb;
            lockstep.WriteVector3(rb.position);
            lockstep.WriteQuaternion(rb.rotation);
            lockstep.WriteVector3(rb.velocity);
            lockstep.WriteVector3(rb.angularVelocity);
            SendExtensionDataInputAction(nameof(OnRigidbodyUpdateIA));
        }

        [EntityExtensionDataInputAction]
        public void OnRigidbodyUpdateIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  OnRigidbodyUpdateIA");
#endif
            if (lockstep.SendingPlayerId != responsiblePlayerId)
                return;
            position = lockstep.ReadVector3();
            rotation = lockstep.ReadQuaternion();
            velocity = lockstep.ReadVector3();
            angularVelocity = lockstep.ReadVector3();
            if (ext != null && responsiblePlayerId != localPlayerId)
                ext.ApplyDataToRigidbody();
        }

        public override void Serialize(bool isExport)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  Serialize");
#endif
            if (!isExport)
                lockstep.WriteSmallUInt(responsiblePlayerId);
            lockstep.WriteVector3(position);
            lockstep.WriteQuaternion(rotation);
            lockstep.WriteVector3(velocity);
            lockstep.WriteVector3(angularVelocity);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] PhysicsEntityExtensionData  Deserialize");
#endif
            if (!isImport)
            {
                responsiblePlayerId = lockstep.ReadSmallUInt();
                manager.RegisterPhysicsExtensionData(this);
            }
            position = lockstep.ReadVector3();
            rotation = lockstep.ReadQuaternion();
            velocity = lockstep.ReadVector3();
            angularVelocity = lockstep.ReadVector3();
        }
    }
}
