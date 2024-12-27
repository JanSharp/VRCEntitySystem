using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class Prototype : UdonSharpBehaviour
    {
        [HideInInspector] [SerializeField] private uint id;
        public uint Id => id;
        public abstract string PrototypeName { get; }
        public abstract string DisplayName { get; }
        public abstract string ShortDescription { get; }
        public abstract string LongDescription { get; }
    }
}
