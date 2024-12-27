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
            counterValue = Data.counterValue;
            UpdateText();
        }

        private void UpdateText()
        {
            text.text = counterValue.ToString();
        }

        public void OnDecrementClick()
        {
            lockstep.WriteSmallInt(-1);
            SendExtensionInputAction(nameof(OnModifyValueIA));
        }

        public void OnIncrementClick()
        {
            lockstep.WriteSmallInt(1);
            SendExtensionInputAction(nameof(OnModifyValueIA));
        }

        [EntityExtensionInputAction]
        public void OnModifyValueIA()
        {
            int delta = lockstep.ReadSmallInt();
            Data.counterValue += delta;
            counterValue += delta;
            UpdateText();
        }
    }
}
