using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DestroyEntityOnInteract : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][FindInParent] private Entity entity;

        public override void Interact() => entity.SendDestroyEntityIA();
    }
}
