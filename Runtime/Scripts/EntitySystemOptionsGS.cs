using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    // Must be its own game state because the entity system game state must load after player data
    // while options must load before in order to be available for custom player data.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [LockstepGameStateDependency(typeof(PlayerDataManagerAPI), SelfLoadsBeforeDependency = true)]
    public class EntitySystemOptionsGS : LockstepGameState
    {
        public override string GameStateInternalName => "jansharp.entity-system-options";
        public override string GameStateDisplayName => "Entity System Options";
        public override bool GameStateSupportsImportExport => true;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;
        [SerializeField] private EntitySystemImportExportOptionsUI exportUI;
        [SerializeField] private EntitySystemImportExportOptionsUI importUI;
        public override LockstepGameStateOptionsUI ExportUI => exportUI;
        public override LockstepGameStateOptionsUI ImportUI => importUI;

        public EntitySystemImportExportOptions ExportOptions => (EntitySystemImportExportOptions)OptionsForCurrentExport;
        public EntitySystemImportExportOptions ImportOptions => (EntitySystemImportExportOptions)OptionsForCurrentImport;
        private EntitySystemImportExportOptions optionsFromExport;
        public EntitySystemImportExportOptions OptionsFromExport => optionsFromExport;

        public override void SerializeGameState(bool isExport, LockstepGameStateOptionsData exportOptions)
        {
            if (!isExport)
                return;
            lockstep.WriteCustomClass(exportOptions);
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion, LockstepGameStateOptionsData importOptions)
        {
            if (!isImport)
                return null;
            optionsFromExport = (EntitySystemImportExportOptions)lockstep.ReadCustomClass(nameof(EntitySystemImportExportOptions));
            return null;
        }

        [LockstepEvent(LockstepEventType.OnImportFinished, Order = 10000)]
        public virtual void OnImportFinished()
        {
            if (!IsPartOfCurrentImport)
                return;
            optionsFromExport.DecrementRefsCount();
            optionsFromExport = null;
        }
    }
}
