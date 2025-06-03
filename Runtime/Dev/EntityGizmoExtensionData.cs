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

        public override void InitFromDefault(EntityExtension entityExtension)
        {
        }

        public override void InitFromPreInstantiated(EntityExtension entityExtension)
        {
        }

        public override void OnAssociatedWithExtension()
        {
        }

        public override void Serialize(bool isExport)
        {
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
        }
    }
}
