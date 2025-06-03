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
        private uint localPlayerId;

        public override void WannaBeConstructor()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  InitFromDefault");
#endif
            localPlayerId = (uint)Networking.LocalPlayer.playerId;
        }

        public override void InitFromDefault(EntityExtension entityExtension)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  InitFromDefault");
#endif
            MyCounterEntityExtension extension = (MyCounterEntityExtension)entityExtension;
            counterValue = extension.counterValue;
        }

        public override void InitFromPreInstantiated(EntityExtension entityExtension)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  InitFromPreInstantiated");
#endif
            MyCounterEntityExtension extension = (MyCounterEntityExtension)entityExtension;
            counterValue = extension.counterValue;
        }

        public override void OnAssociatedWithExtension()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  OnAssociatedWithExtension");
#endif
        }

        public void SendModifyValueIA(int delta)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  SendModifyValueIA");
#endif
            lockstep.WriteSmallInt(delta);
            SendExtensionDataInputAction(nameof(OnModifyValueIA));
            if (extension == null)
                return;
            Extension.counterValue += delta;
            Extension.UpdateText();
        }

        [EntityExtensionDataInputAction]
        public void OnModifyValueIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  OnModifyValueIA");
#endif
            int delta = lockstep.ReadSmallInt();
            counterValue += delta;
            if (lockstep.SendingPlayerId == localPlayerId || extension == null)
                return;
            Extension.counterValue += delta;
            Extension.UpdateText();
        }

        public override void Serialize(bool isExport)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  Serialize");
#endif
            lockstep.WriteSmallInt(counterValue);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtensionData  Deserialize");
#endif
            counterValue = lockstep.ReadSmallInt();
        }
    }
}
