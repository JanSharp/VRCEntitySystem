using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("4fdf8f8b2fe34c67ea5fa957a3029abe")] // Runtime/Prefabs/PhysicsEntityManager.prefab
    public class PhysicsEntityTransformController : EntityTransformController
    {
        public override void OnControlLost(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityTransformController  OnControlLost");
#endif
            PhysicsEntityExtensionData data = entityData.GetExtensionData<PhysicsEntityExtensionData>(nameof(PhysicsEntityExtensionData));
            data.GoToSleep();
        }

        public override void OnControlTakenOver(EntityData entityData, EntityTransformController newController)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityTransformController  OnControlTakenOver");
#endif
            OnControlLost(entityData);
        }

        public override void OnLatencyControlLost(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityTransformController  OnLatencyControlLost");
#endif
            PhysicsEntityExtension ext = entity.GetExtension<PhysicsEntityExtension>(nameof(PhysicsEntityExtensionData));
            Transform t = ext.entity.transform;
            ext.GoToSleep(t.position, t.rotation);
        }

        public override void OnLatencyControlTakenOver(Entity entity, EntityTransformController newController)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityTransformController  OnLatencyControlTakenOver");
#endif
            OnLatencyControlLost(entity);
        }

        public override bool TryGetGameStatePosition(EntityData entityData, out Vector3 position)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityTransformController  TryGetGameStatePosition");
#endif
            position = entityData.position;
            return true;
        }

        public override bool TryGetGameStateRotation(EntityData entityData, out Quaternion rotation)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityTransformController  TryGetGameStateRotation");
#endif
            rotation = entityData.rotation;
            return true;
        }

        public override bool TryGetGameStateScale(EntityData entityData, out Vector3 scale)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] PhysicsEntityTransformController  TryGetGameStateScale");
#endif
            scale = entityData.scale;
            return true;
        }
    }
}
