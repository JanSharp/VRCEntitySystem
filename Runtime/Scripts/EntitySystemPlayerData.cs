using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntitySystemPlayerData : PlayerData
    {
        public override string PlayerDataInternalName => "jansharp.entity-system-player-data";
        public override string PlayerDataDisplayName => "Entity System Player Data";
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        #region Game State
        [System.NonSerialized] public uint createdEntitiesCount = 0u;
        [System.NonSerialized] public uint lastUsedEntitiesCount = 0u;
        /// <summary>
        /// <para>While this is part of the game state, the order is non deterministic. Be very careful with
        /// how this is used to affect the game state.</para>
        /// </summary>
        [System.NonSerialized] public PhysicsEntityExtensionData[] managedPhysicsEntities = new PhysicsEntityExtensionData[ArrList.MinCapacity];
        [System.NonSerialized] public int managedPhysicsEntitiesCount;
        #endregion

        public void GainCreated(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  GainCreated");
#endif
            createdEntitiesCount++;
        }

        public void LoseCreated(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseCreated");
#endif
            createdEntitiesCount--;
        }

        public void GainLastUsed(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  GainLastUsed");
#endif
            lastUsedEntitiesCount++;
        }

        public void LoseLastUsed(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseLastUsed");
#endif
            lastUsedEntitiesCount--;
        }

        public void GainResponsibility(PhysicsEntityExtensionData physicsEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  GainResponsibility");
#endif
            ArrList.Add(ref managedPhysicsEntities, ref managedPhysicsEntitiesCount, physicsEntityData);
        }

        public void GainResponsibility(PhysicsEntityExtensionData[] other, int otherCount)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  GainResponsibility");
#endif
            ArrList.AddRange(ref managedPhysicsEntities, ref managedPhysicsEntitiesCount, other, otherCount);
        }

        public void LoseResponsibility(PhysicsEntityExtensionData physicsEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseResponsibility");
#endif
            ArrList.Remove(ref managedPhysicsEntities, ref managedPhysicsEntitiesCount, physicsEntityData);
        }

        public void LoseAllResponsibility()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseAllResponsibility");
#endif
            ArrList.Clear(ref managedPhysicsEntities, ref managedPhysicsEntitiesCount);
        }

        private void Clear()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  Clear");
#endif
            createdEntitiesCount = 0;
            lastUsedEntitiesCount = 0;
            LoseAllResponsibility();
        }

        public override bool PersistPlayerDataWhileOffline()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  PersistPlayerDataWhileOffline");
#endif
            return createdEntitiesCount != 0u || lastUsedEntitiesCount != 0u || managedPhysicsEntitiesCount != 0;
        }

        public override void Serialize(bool isExport)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  Serialize");
#endif
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  Deserialize");
#endif
            // The entire player data state gets restored through the deserialization of the EntitySystem
            // game state.
            Clear();
        }
    }
}
