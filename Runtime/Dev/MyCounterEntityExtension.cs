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
        public MyCounterEntityExtensionData ExtensionData => (MyCounterEntityExtensionData)extensionData;
        public int counterValue;
        public TextMeshProUGUI text;

        public override void InitFromExtensionData()
        {
            counterValue = ExtensionData.counterValue;
            UpdateText();
        }

        private void UpdateText()
        {
            text.text = counterValue.ToString();
        }

        public void OnIncrementClick()
        {
        }

        public void OnDecrementClick()
        {
        }
    }
}
