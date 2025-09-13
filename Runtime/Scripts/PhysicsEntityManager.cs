using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("4fdf8f8b2fe34c67ea5fa957a3029abe")] // Runtime/Prefabs/PhysicsEntityManager.prefab
    public class PhysicsEntityManager : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private LockstepAPI lockstep;
        [HideInInspector][SerializeField][SingletonReference] private EntitySystem entitySystem;

        [LockstepEvent(LockstepEventType.OnClientLeft)]
        public void OnClientLeft()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityManager  OnClientLeft");
#endif
            EntitySystemPlayerData leftPlayerData = entitySystem.GetPlayerDataForPlayerId(lockstep.LeftPlayerId);
            PhysicsEntityExtensionData[] managed = leftPlayerData.managedPhysicsEntities;
            int managedCount = leftPlayerData.managedPhysicsEntitiesCount;
            if (managedCount == 0)
                return;
            EntitySystemPlayerData masterPlayerData = entitySystem.GetPlayerDataForPlayerId(lockstep.MasterPlayerId);
            masterPlayerData.GainResponsibility(managed, managedCount);
            for (int i = 0; i < managedCount; i++)
            {
                PhysicsEntityExtensionData extensionData = managed[i];
                // Must not use SetResponsiblePlayerId as that would call the deregister and register
                // functions in the manager here, but the responsibilities have already been managed.
                extensionData.responsiblePlayerId = masterPlayerData.PlayerId;
                if (!extensionData.entityData.ResetLatencyStateIfItDiverged() && extensionData.ext != null)
                    extensionData.ext.SetResponsiblePlayerId(masterPlayerData.PlayerId);
            }
            leftPlayerData.LoseAllResponsibility();
        }

        public void RegisterPhysicsExtensionData(PhysicsEntityExtensionData extensionData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityManager  RegisterPhysicsExtensionData");
#endif
            if (extensionData.responsiblePlayerId == 0u)
                return;
            EntitySystemPlayerData playerData = entitySystem.GetPlayerDataForPlayerId(extensionData.responsiblePlayerId);
            playerData.GainResponsibility(extensionData);
        }

        public void DeregisterPhysicsExtensionData(PhysicsEntityExtensionData extensionData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityManager  DeregisterPhysicsExtensionData");
#endif
            if (extensionData.responsiblePlayerId == 0u)
                return;
            EntitySystemPlayerData playerData = entitySystem.GetPlayerDataForPlayerId(extensionData.responsiblePlayerId);
            playerData.LoseResponsibility(extensionData);
        }

        // TODO: The manager could handle all of the update loops for all entities associated with the local player. This could make spreading work out more reliable
    }
}
