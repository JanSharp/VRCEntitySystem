using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MyCounterEntityExtension : EntityExtension
    {
        public MyCounterEntityExtensionData Data => (MyCounterEntityExtensionData)extensionData;
        public int counterValue;
        public TextMeshProUGUI text;

        public override void InitFromExtensionData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  InitFromExtensionData");
            #endif
            counterValue = Data.counterValue;
            UpdateText();
        }

        private void UpdateText()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  UpdateText");
            #endif
            text.text = counterValue.ToString();
        }

        public void OnDecrementClick()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  OnDecrementClick");
            #endif
            lockstep.WriteSmallInt(-1);
            SendExtensionInputAction(nameof(OnModifyValueIA));
        }

        public void OnIncrementClick()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  OnIncrementClick");
            #endif
            lockstep.WriteSmallInt(1);
            SendExtensionInputAction(nameof(OnModifyValueIA));
        }

        [EntityExtensionInputAction]
        public void OnModifyValueIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  OnModifyValueIA");
            #endif
            int delta = lockstep.ReadSmallInt();
            Data.counterValue += delta;
            counterValue += delta;
            UpdateText();
        }
    }
}
