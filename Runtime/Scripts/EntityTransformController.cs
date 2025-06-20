using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityTransformController : UdonSharpBehaviour
    {
        public abstract void OnControlTakenOver(EntityData entityData, EntityTransformController newController);
        public abstract void OnControlLost(EntityData entityData);
        // These 2 latency ones should not be used to send input actions to subsequently modify the game state.
        // That gets really awkward when an import happened which then could raise these events through a
        // ApplyExtensionData call. However it's an import, so the previous state should be overwritten,
        // therefore there shouldn't be an IA sent from these as that would do exactly that - the previous
        // state before the import having effect on the game state after the import.
        public abstract void OnLatencyControlTakenOver(Entity entity, EntityTransformController newController);
        public abstract void OnLatencyControlLost(Entity entity);
        public abstract bool TryGetGameStatePosition(EntityData entityData, out Vector3 position);
        public abstract bool TryGetGameStateRotation(EntityData entityData, out Quaternion rotation);
        public abstract bool TryGetGameStateScale(EntityData entityData, out Vector3 scale);
    }
}
