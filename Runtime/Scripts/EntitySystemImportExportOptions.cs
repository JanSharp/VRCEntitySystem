using UdonSharp;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntitySystemImportExportOptions : LockstepGameStateOptionsData
    {
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        [System.NonSerialized] public bool includeEntities = true;

        public override LockstepGameStateOptionsData Clone()
        {
            var clone = WannaBeClasses.New<EntitySystemImportExportOptions>(nameof(EntitySystemImportExportOptions));
            clone.includeEntities = includeEntities;
            return clone;
        }

        public override void Serialize(bool isExport)
        {
            lockstep.WriteFlags(includeEntities);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
            lockstep.ReadFlags(out includeEntities);
        }
    }
}
