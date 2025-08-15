using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("4fdf8f8b2fe34c67ea5fa957a3029abe")] // Runtime/Prefabs/PhysicsEntityManager.prefab
    public class PhysicsEntityManager : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private LockstepAPI lockstep;

        /// <summary>
        /// <para><see cref="uint"/> playerId => <see cref="DataList"/> of <see cref="PhysicsEntityExtensionData"/></para>
        /// </summary>
        private DataDictionary associatedEntitiesLut = new DataDictionary();

        private DataList GetExtensionsListForPlayerId(uint playerId)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityManager  GetExtensionsListForPlayerId");
#endif
            if (associatedEntitiesLut.TryGetValue(playerId, out DataToken listToken))
                return listToken.DataList;
            DataList extensionsList = new DataList();
            associatedEntitiesLut.Add(playerId, extensionsList);
            return extensionsList;
        }

        [LockstepEvent(LockstepEventType.OnClientLeft)]
        public void OnClientLeft()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityManager  OnClientLeft");
#endif
            if (!associatedEntitiesLut.Remove(lockstep.LeftPlayerId, out DataToken listToken))
                return;
            uint masterPlayerId = lockstep.MasterPlayerId;
            DataList listToCollapse = listToken.DataList;
            DataList destinationList = GetExtensionsListForPlayerId(masterPlayerId);
            destinationList.AddRange(listToCollapse);
            int count = listToCollapse.Count;
            for (int i = 0; i < count; i++)
            {
                PhysicsEntityExtensionData extensionData = (PhysicsEntityExtensionData)listToCollapse[i].Reference;
                extensionData.responsiblePlayerId = masterPlayerId;
                if (extensionData.ext != null)
                    extensionData.ext.UpdateUpdateLoopRunningState();
            }
        }

        public void RegisterPhysicsExtensionData(PhysicsEntityExtensionData extensionData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityManager  RegisterPhysicsExtensionData");
#endif
            GetExtensionsListForPlayerId(extensionData.responsiblePlayerId).Add(extensionData);
        }

        public void DeregisterPhysicsExtensionData(PhysicsEntityExtensionData extensionData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityManager  DeregisterPhysicsExtensionData");
#endif
            GetExtensionsListForPlayerId(extensionData.responsiblePlayerId).Remove(extensionData);
        }

        // TODO: The manager could handle all of the update loops for all entities associated with the local player. This could make spreading work out more reliable
    }
}
