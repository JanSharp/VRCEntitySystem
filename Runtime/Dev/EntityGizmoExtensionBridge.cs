﻿using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityGizmoExtensionBridge : TransformGizmoBridge
    {
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

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!isInVR || args.handType != HandType.RIGHT)
                return;
            if (value)
                gizmo.Activate();
            else
                gizmo.Deactivate();
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
            currentEntity.FlagForPositionChange();
        }

        public override void OnRotationModified()
        {
            currentEntity.FlagForRotationChange();
        }

        public override void OnScaleModified()
        {
            currentEntity.FlagForScaleChange();
        }
    }
}
