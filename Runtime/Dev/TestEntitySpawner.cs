using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TestEntitySpawner : UdonSharpBehaviour
    {
        [HideInInspector] [SerializeField] [SingletonReference] private EntitySystem entitySystem;

        public string entityPrototypeName;

        public override void Interact()
        {
            if (!entitySystem.TryGetEntityPrototype(entityPrototypeName, out EntityPrototype prototype))
                return;
            entitySystem.SendCreateEntityIA(prototype.Id);
        }
    }
}
