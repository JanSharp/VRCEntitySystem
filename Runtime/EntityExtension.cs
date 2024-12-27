using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityExtension : UdonSharpBehaviour
    {
        public EntityExtensionData extensionData;

        public abstract void InitFromExtensionData();
    }
}
