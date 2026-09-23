using UnityEngine;

namespace JanSharp
{
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    public abstract class EntitySystemAPI : LockstepGameState
    {
        public const ulong InvalidUniqueId = 0uL;
        public const uint InvalidId = 0u;

        public abstract EntitySystemImportExportOptions ExportOptions { get; }
        public abstract EntitySystemImportExportOptions ImportOptions { get; }
        public abstract EntitySystemImportExportOptions OptionsFromExport { get; }

        public abstract int PlayerDataClassNameIndex { get; }
        /// <summary>
        /// <para>Can even get the player data for the local client inside of OnClientBeginCatchUp, because
        /// the PlayerData system creates the player data in OnPreClientJoined, thus making it apart of the
        /// late joiner data that has been sent to the local client. No edge cases! Lockstep is
        /// beautiful.</para>
        /// </summary>
        /// <param name="playerId">Can be <c>0u</c>.</param>
        /// <returns></returns>
        public abstract EntitySystemPlayerData GetPlayerDataForPlayerId(uint playerId);
        /// <inheritdoc cref="GetPlayerDataForPlayerId(uint)"/>
        public abstract EntitySystemPlayerData GetPlayerDataForPersistentId(uint persistentId);

        /// <summary>
        /// </summary>
        /// <param name="core">Must not be <see langword="null"/>.</param>
        /// <returns></returns>
        public abstract EntitySystemPlayerData GetPlayerData(CorePlayerData core);
        public abstract void WritePlayerDataRef(EntitySystemPlayerData playerData);
        public abstract EntitySystemPlayerData ReadPlayerDataRef(bool isImport);

        public abstract bool IsPreInstantiatedEntityId(uint id, bool isImport);

        public abstract EntityPrototype[] EntityPrototypes { get; }
        public abstract EntityPrototype GetEntityPrototype(uint prototypeId);
        public abstract bool TryGetEntityPrototype(uint prototypeId, out EntityPrototype entityPrototype);
        public abstract EntityPrototype GetEntityPrototype(string prototypeName);
        public abstract bool TryGetEntityPrototype(string prototypeName, out EntityPrototype entityPrototype);

        public abstract EntityData GetEntityData(uint entityId);
        public abstract bool TryGetEntityData(uint entityId, out EntityData entityData);

        public abstract EntityData SendCreateEntityIA(uint prototypeId, Vector3 position, Quaternion rotation);
        public abstract EntityData SendCustomCreateEntityIA(uint iaId, uint prototypeId, Vector3 position, Quaternion rotation);
        public abstract EntityData ReadEntityInCustomCreateEntityIA(bool onEntityCreatedGetsRaisedLater = false);
        public abstract void RaiseOnEntityCreatedInCustomCreateEntityIA(EntityData entityData);

        public abstract void WriteEntityPrototypeRef(EntityPrototype prototype);
        public abstract bool TryReadEntityPrototypeRef(out EntityPrototype prototype);
        public abstract bool TryReadEntityPrototypeRef(out EntityPrototype prototype, bool isImport);
        public abstract EntityPrototype ReadEntityPrototypeRef();
        public abstract EntityPrototype ReadEntityPrototypeRef(bool isImport);

        public abstract void WriteEntityDataRef(EntityData entityData);
        public abstract bool TryReadEntityDataRef(out EntityData entityData);
        public abstract bool TryReadEntityDataRef(out EntityData entityData, bool isImport);

        public abstract void SendDestroyEntityIA(EntityData entityData);
        public abstract void SendDestroyAllEntitiesIA();
        public abstract void DestroyEntityInGS(EntityData entityData);

        public abstract void WriteEntityExtensionDataRef(EntityExtensionData extensionData);
        public abstract EntityExtensionData ReadEntityExtensionDataRefDynamic();
        public abstract ulong SendExtensionDataInputAction(EntityExtensionData extensionData, string methodName);

        public abstract EntityPrototypeMetadata GetImportedPrototypeMetadata(uint prototypeId);
        public abstract bool TryGetImportedPrototypeMetadata(uint prototypeId, out EntityPrototypeMetadata metadata);

        public abstract EntityData GetRemappedImportedEntityData(uint importedId);
        public abstract bool TryGetRemappedImportedEntityData(uint importedId, out EntityData remappedEntityData);

        public abstract EntityData DeserializedEntityData { get; }
        public abstract EntityData CreatedEntityData { get; }
        public abstract EntityData DestroyedEntityData { get; }
    }

    public static class EntitySystemAPIExtension
    {
        public static T ReadEntityExtensionDataRef<T>(this EntitySystemAPI entitySystem)
            where T : EntityExtensionData
        {
            return (T)entitySystem.ReadEntityExtensionDataRefDynamic();
        }
    }
}
