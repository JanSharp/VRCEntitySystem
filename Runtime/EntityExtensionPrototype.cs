using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityExtensionPrototype : Prototype
    {
        [SerializeField] private string prototypeName;
        [SerializeField] private string displayName;
        [SerializeField] private string shortDescription;
        [SerializeField] private string longDescription;
        [SerializeField] private GameObject entityExtensionPrefab;
        [SerializeField] private string extensionDataClassName;

        public override string PrototypeName => prototypeName;
        public override string DisplayName => displayName;
        public override string ShortDescription => shortDescription;
        public override string LongDescription => longDescription;
        public GameObject EntityExtensionPrefab => entityExtensionPrefab;
        public string ExtensionDataClassName => extensionDataClassName;
    }
}
