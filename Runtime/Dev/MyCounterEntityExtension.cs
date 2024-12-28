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
        private ulong[] latencyHiddenUniqueIds = new ulong[ArrList.MinCapacity];
        private int latencyHiddenUniqueIdsCount = 0;
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
            SendModifyValueIA(-1);
        }

        public void OnIncrementClick()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  OnIncrementClick");
            #endif
            SendModifyValueIA(1);
        }

        private void SendModifyValueIA(int delta)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  SendModifyValueIA");
            #endif
            lockstep.WriteSmallInt(delta);
            ulong uniqueIds = SendExtensionInputAction(nameof(OnModifyValueIA));
            ArrList.Add(ref latencyHiddenUniqueIds, ref latencyHiddenUniqueIdsCount, uniqueIds);
            counterValue += delta;
            UpdateText();
        }

        [EntityExtensionInputAction]
        public void OnModifyValueIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] MyCounterEntityExtension  OnModifyValueIA");
            #endif
            int delta = lockstep.ReadSmallInt();
            Data.counterValue += delta;
            if (ArrList.Remove(ref latencyHiddenUniqueIds, ref latencyHiddenUniqueIdsCount, lockstep.SendingUniqueId) == -1)
            {
                counterValue += delta;
                UpdateText();
            }
        }
    }
}
