using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityGizmoExtensionBridge : TransformGizmoBridge
    {
        [HideInInspector] [SerializeField] [SingletonReference] private EntitySystem entitySystem;
        public TransformGizmo gizmo;
        private bool isTracking = false;
        private Entity currentEntity;
        public Entity CurrentEntity
        {
            get => currentEntity;
            set
            {
                if (value == null && isTracking)
                {
                    SendMovementIA();
                    SendScaleIA();
                    currentEntity = null; // After sending.
                    isTracking = false;
                    gizmo.SetTracked(null, null);
                    return;
                }
                if (value == currentEntity)
                    return;
                currentEntity = value;
                isTracking = true;
                gizmo.SetTracked(currentEntity.transform, this);
            }
        }

        private VRCPlayerApi localPlayer;
        private bool isInVR;

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();
        }

        private void Update()
        {
            if (CurrentEntity == null)
            {
                if (isTracking)
                    CurrentEntity = null;
                return;
            }
            if (Input.GetMouseButtonDown(0))
                gizmo.Activate();
            if (Input.GetMouseButtonUp(0))
                gizmo.Deactivate();
            if (Input.GetKeyDown(KeyCode.Q))
                CurrentEntity = null;
        }

        public override void GetHead(out Vector3 position, out Quaternion rotation)
        {
            VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            position = head.position;
            rotation = head.rotation;
        }

        public override void GetRaycastOrigin(out Vector3 position, out Quaternion rotation)
        {
            if (isInVR)
            {
                VRCPlayerApi.TrackingData hand = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
                position = hand.position;
                rotation = hand.rotation;
            }
            else
            {
                VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                position = head.position;
                rotation = head.rotation;
            }
        }

        public override bool ActivateThisFrame()
        {
            // if (!isInVR)
            //     return Input.GetMouseButtonDown(0);
            return false;
        }

        public override bool DeactivateThisFrame()
        {
            // if (!isInVR)
            //     return Input.GetMouseButtonUp(0);
            return false;
        }

        public override bool SnappingThisFrame()
        {
            if (!isInVR)
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            return false;
        }

        public override void OnPositionModified()
        {
            EnqueueMovementIA();
        }

        public override void OnRotationModified()
        {
            EnqueueMovementIA();
        }

        public override void OnScaleModified()
        {
            EnqueueScaleIA();
        }

        private void EnqueueMovementIA()
        {
            if (isMovementIAQueued)
                return;
            isMovementIAQueued = true;
            SendCustomEventDelayedSeconds(nameof(SendMovementIA), 0.39f);
        }

        private bool isMovementIAQueued = false;
        public void SendMovementIA()
        {
            if (CurrentEntity == null)
                return;
            isMovementIAQueued = false;
            entitySystem.SendMoveEntityIA(CurrentEntity.entityData.id, CurrentEntity.transform.position, CurrentEntity.transform.rotation);
        }

        private void EnqueueScaleIA()
        {
            if (isScaleIAQueued)
                return;
            isScaleIAQueued = true;
            SendCustomEventDelayedSeconds(nameof(SendScaleIA), 0.39f);
        }

        private bool isScaleIAQueued = false;
        public void SendScaleIA()
        {
            if (CurrentEntity == null)
                return;
            isScaleIAQueued = false;
            entitySystem.SendSetEntityScaleIA(CurrentEntity.entityData.id, CurrentEntity.transform.localScale);
        }
    }
}
