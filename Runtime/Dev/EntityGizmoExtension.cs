using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [AssociatedEntityExtensionData(typeof(EntityGizmoExtensionData))]
    public class EntityGizmoExtension : EntityExtension
    {
        private EntityGizmoExtensionBridge gizmoBridge;

        private void Start()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityGizmoExtension  Start");
            #endif
            gizmoBridge = GameObject.Find("EntityGizmoExtensionBridge").GetComponent<EntityGizmoExtensionBridge>();
        }

        public override void ApplyExtensionData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityGizmoExtension  ApplyExtensionData");
            #endif
        }

        public override void Interact()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityGizmoExtension  Interact");
            #endif
            if (entity.entityData == null)
                return;
            gizmoBridge.CurrentEntity = gizmoBridge.CurrentEntity == entity ? null : entity;
        }

        public override void DisassociateFromExtensionDataAndReset(EntityExtension defaultExtension)
        {
        }
    }
}
