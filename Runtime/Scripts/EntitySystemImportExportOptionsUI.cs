using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntitySystemImportExportOptionsUI : LockstepGameStateOptionsUI
    {
        public override string OptionsClassName => nameof(EntitySystemImportExportOptions);

        [SerializeField] private bool isImportUI;

        private EntitySystemImportExportOptions currentOptions;
        private EntitySystemImportExportOptions optionsToValidate;

        private ToggleFieldWidgetData includeEntitiesToggle;

        protected override LockstepGameStateOptionsData NewOptionsImpl()
        {
            return wannaBeClasses.New<EntitySystemImportExportOptions>(nameof(EntitySystemImportExportOptions));
        }

        protected override void ValidateOptionsImpl()
        {
        }

        protected override void UpdateCurrentOptionsFromWidgetsImpl()
        {
            if (includeEntitiesToggle.Interactable)
                currentOptions.includeEntities = includeEntitiesToggle.Value;
        }

        protected override void InitWidgetData()
        {
        }

        private void LazyInitWidgetData()
        {
            if (includeEntitiesToggle != null)
                return;
            includeEntitiesToggle = widgetManager.NewToggleField("Entities - Objects/Items", false);
        }

        protected override void OnOptionsEditorShow(LockstepOptionsEditorUI ui, uint importedDataVersion)
        {
            LazyInitWidgetData();
            if (isImportUI)
            {
                var optionsFromExport = (EntitySystemImportExportOptions)lockstep.ReadCustomClass(nameof(EntitySystemImportExportOptions), isImport: true);
                includeEntitiesToggle.Interactable = optionsFromExport.includeEntities;
                optionsFromExport.Delete();
            }
            includeEntitiesToggle.SetValueWithoutNotify(includeEntitiesToggle.Interactable && currentOptions.includeEntities);
            ui.General.AddChildDynamic(includeEntitiesToggle);
        }

        protected override void OnOptionsEditorHide(LockstepOptionsEditorUI ui)
        {
        }
    }
}
