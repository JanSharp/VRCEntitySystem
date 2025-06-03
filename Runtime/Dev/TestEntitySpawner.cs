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
        public Transform spawnLocation;

        public override void Interact()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] TestEntitySpawner  Interact");
            #endif
            if (!entitySystem.TryGetEntityPrototype(entityPrototypeName, out EntityPrototype prototype))
                return;
            for (int i = 0; i < 16; i++)
            {
                entitySystem.SendCreateEntityIA(
                    prototype.Id,
                    spawnLocation.position
                        + Vector3.right * Random.Range(0f, 10f)
                        + Vector3.up * Random.Range(0f, 2f),
                    spawnLocation.rotation);
            }
        }
    }
}
