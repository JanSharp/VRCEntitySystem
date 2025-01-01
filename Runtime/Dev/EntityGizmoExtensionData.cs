using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityGizmoExtensionData : EntityExtensionData
    {
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
        }

        public override void InitFromExtension()
        {
        }

        public override void Serialize(bool isExport)
        {
        }
    }
}
