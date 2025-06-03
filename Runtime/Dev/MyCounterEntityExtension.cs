using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [AssociatedEntityExtensionData(typeof(MyCounterEntityExtensionData))]
    public class MyCounterEntityExtension : EntityExtension
    {
        public MyCounterEntityExtensionData Data => (MyCounterEntityExtensionData)extensionData;
        /// <summary>
        /// <para>latency state.</para>
        /// </summary>
        public int counterValue;
        public TextMeshProUGUI text;

        public override void ApplyExtensionData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  ApplyExtensionData");
#endif
            counterValue = Data.counterValue;
            UpdateText();
        }

        public override void DisassociateFromExtensionDataAndReset(EntityExtension defaultExtension)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  DisassociateFromExtensionDataAndReset");
#endif
            MyCounterEntityExtension extension = (MyCounterEntityExtension)defaultExtension;
            counterValue = extension.counterValue;
            UpdateText();
        }

        public void UpdateText()
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
            Data.SendModifyValueIA(-1);
        }

        public void OnIncrementClick()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  OnIncrementClick");
#endif
            Data.SendModifyValueIA(1);
        }
    }
}
