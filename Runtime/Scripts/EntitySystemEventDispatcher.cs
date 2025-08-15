using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [CustomRaisedEventsDispatcher(typeof(EntitySystemEventAttribute), typeof(EntitySystemEventType))]
    public partial class EntitySystem : LockstepGameState
    {
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onEntityDeserializedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onEntityCreatedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onEntityDestroyedListeners;

        private EntityData deserializedEntityData;
        public EntityData DeserializedEntityData => deserializedEntityData;
        private void RaiseOnEntityDeserialized(EntityData deserializedEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityDeserialized");
#endif
            this.deserializedEntityData = deserializedEntityData;
            CustomRaisedEvents.Raise(ref onEntityDeserializedListeners, nameof(EntitySystemEventType.OnEntityDeserialized));
            this.deserializedEntityData = null;
        }

        private EntityData createdEntityData;
        public EntityData CreatedEntityData => createdEntityData;
        private void RaiseOnEntityCreated(EntityData createdEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityCreated");
#endif
            this.createdEntityData = createdEntityData;
            CustomRaisedEvents.Raise(ref onEntityCreatedListeners, nameof(EntitySystemEventType.OnEntityCreated));
            this.createdEntityData = null;
        }

        private EntityData destroyedEntityData;
        public EntityData DestroyedEntityData => destroyedEntityData;
        private void RaiseOnEntityDestroyed(EntityData destroyedEntityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntitySystem  RaiseOnEntityDestroyed");
#endif
            this.destroyedEntityData = destroyedEntityData;
            CustomRaisedEvents.Raise(ref onEntityDestroyedListeners, nameof(EntitySystemEventType.OnEntityDestroyed));
            this.destroyedEntityData = null;
        }
    }
}
