using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityPrototype : Prototype
    {
        [SerializeField] private string prototypeName;
        [SerializeField] private string displayName;
        [SerializeField] private string shortDescription;
        [SerializeField] private string longDescription;
        [SerializeField] private GameObject entityPrefab;
        [SerializeField] private uint[] localExtensionIds;
        [SerializeField] private string[] extensionClassNames;

        public override string PrototypeName => prototypeName;
        public override string DisplayName => displayName;
        public override string ShortDescription => shortDescription;
        public override string LongDescription => longDescription;
        public GameObject EntityPrefab => entityPrefab;
        public uint[] LocalExtensionIds => localExtensionIds;
        public string[] ExtensionClassNames => extensionClassNames;
    }
}
