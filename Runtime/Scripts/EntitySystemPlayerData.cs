using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntitySystemPlayerData : CustomPlayerData
    {
        public override string PlayerDataInternalName => "jansharp.entity-system-player-data";
        public override string PlayerDataDisplayName => "Entity System Player Data";
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        [HideInInspector][SerializeField][SingletonReference] private EntitySystem entitySystem;

        #region Game State
        /// <summary>
        /// <para>While this is part of the game state, the order is non deterministic. Be very careful with
        /// how this is used to affect the game state.</para>
        /// </summary>
        [System.NonSerialized] public EntityData[] createdEntities = new EntityData[ArrList.MinCapacity];
        [System.NonSerialized] public int createdEntitiesCount = 0;
        /// <inheritdoc cref="createdEntities"/>
        [System.NonSerialized] public EntityData[] lastUsedEntities = new EntityData[ArrList.MinCapacity];
        [System.NonSerialized] public int lastUsedEntitiesCount = 0;
        /// <inheritdoc cref="createdEntities"/>
        [System.NonSerialized] public PhysicsEntityExtensionData[] managedPhysicsEntities = new PhysicsEntityExtensionData[ArrList.MinCapacity];
        [System.NonSerialized] public int managedPhysicsEntitiesCount = 0;
        #endregion

        public override bool WannaBeClassSupportsPooling => true;
        public override void ResetWannaBeClassToDefault()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  ResetWannaBeClassToDefault");
#endif
            base.ResetWannaBeClassToDefault();
            createdEntities = new EntityData[ArrList.MinCapacity];
            createdEntitiesCount = 0;
            lastUsedEntities = new EntityData[ArrList.MinCapacity];
            lastUsedEntitiesCount = 0;
            managedPhysicsEntities = new PhysicsEntityExtensionData[ArrList.MinCapacity];
            managedPhysicsEntitiesCount = 0;
        }

        public override void OnPlayerDataUninit(bool forced)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  OnPlayerDataUninit");
#endif
            for (int i = 0; i < createdEntitiesCount; i++)
                createdEntities[i].CreatedByPlayerData = null;
            for (int i = 0; i < lastUsedEntitiesCount; i++)
                lastUsedEntities[i].LastUserPlayerData = null;
        }

        public void GainCreated(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  GainCreated");
#endif
            ArrList.Add(ref createdEntities, ref createdEntitiesCount, entityData);
        }

        public void LoseCreated(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseCreated");
#endif
            ArrList.Remove(ref createdEntities, ref createdEntitiesCount, entityData);
        }

        public void LoseAllCreated()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseAllCreated");
#endif
            ArrList.Clear(ref createdEntities, ref createdEntitiesCount);
        }

        public void GainLastUsed(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  GainLastUsed");
#endif
            ArrList.Add(ref lastUsedEntities, ref lastUsedEntitiesCount, entityData);
        }

        public void LoseLastUsed(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseLastUsed");
#endif
            ArrList.Remove(ref lastUsedEntities, ref lastUsedEntitiesCount, entityData);
        }

        public void LoseAllLastUsed()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  LoseLastUsed");
#endif
            ArrList.Clear(ref lastUsedEntities, ref lastUsedEntitiesCount);
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
            LoseAllCreated();
            LoseAllLastUsed();
            LoseAllResponsibility();
        }

        public override bool PersistPlayerDataWhileOffline()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  PersistPlayerDataWhileOffline");
#endif
            return createdEntitiesCount != 0 || lastUsedEntitiesCount != 0 || managedPhysicsEntitiesCount != 0;
        }

        public override bool PersistPlayerDataInExport()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystemPlayerData  PersistPlayerDataInExport");
#endif
            return entitySystem.ExportOptions.includeEntities && PersistPlayerDataWhileOffline();
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
