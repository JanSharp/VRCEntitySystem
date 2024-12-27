using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MyCounterEntityExtensionData : EntityExtensionData
    {
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0;
        public override uint LowestSupportedDataVersion => 0;

        public MyCounterEntityExtension Extension => (MyCounterEntityExtension)extension;
        [System.NonSerialized] public int counterValue;

        public override void InitFromExtension()
        {
            counterValue = Extension.counterValue;
        }

        public override void Serialize(bool isExport)
        {
            lockstep.WriteSmallInt(counterValue);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
            counterValue = lockstep.ReadSmallInt();
        }
    }
}
