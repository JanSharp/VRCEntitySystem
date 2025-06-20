using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    public class EntityPooling : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private LockstepAPI lockstep;
        [HideInInspector][SerializeField][SingletonReference] private EntitySystem entitySystem;
        [HideInInspector][SerializeField][SingletonReference] private WannaBeClassesManager wannaBeClasses;
        // private DataDictionary defaultEntities = new DataDictionary();

        private object[][] requestQueue = new object[ArrQueue.MinCapacity][];
        private int rqStartIndex = 0;
        private int rqCount = 0;

        private DataDictionary pooledEntities = new DataDictionary();

        private void Start()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPooling  Start");
#endif
            foreach (EntityPrototype prototype in entitySystem.EntityPrototypes)
                pooledEntities.Add(prototype.Id, new DataList());

            // TODO: Start gradual deletion loop of stale pooled entities.
            // Could use a linked list of all pooled entities, unfortunately would have to be bi bidirectional.
        }

        public void RequestEntity(EntityData entityData, bool highPriority = false)
        {
            RequestEntity(entityData, entityData.position, entityData.rotation, entityData.scale, highPriority);
        }

        public void RequestEntity(
            EntityData entityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool highPriority = false)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPooling  RequestEntity");
#endif
            object[] request = new object[]
            {
                entityData,
                position,
                rotation,
                scale,
            };
            if (highPriority)
                ArrQueue.EnqueueAtFront(ref requestQueue, ref rqStartIndex, ref rqCount, request);
            else
                ArrQueue.Enqueue(ref requestQueue, ref rqStartIndex, ref rqCount, request);
            StartRequestLoop();
        }

        public void ReturnEntity(EntityData entityData)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPooling  ReturnEntity - entityData");
#endif
            entityData.entityIsDestroyed = true;
            Entity entity = entityData.entity;
            if (entity == null)
                return;
            ReturnEntity(entity);
            entityData.OnDisassociateFromEntity();
            entityData.entity = null;
            entity.entityData = null;
        }

        public void ReturnEntity(Entity entity)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPooling  ReturnEntity - entity");
#endif
            entity.gameObject.SetActive(false);
            entity.DisassociateFromEntityDataAndReset(entity.prototype.DefaultEntityInst);
            pooledEntities[entity.prototype.Id].DataList.Add(entity);
        }

        private void StartRequestLoop()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPooling  StartRequestLoop");
#endif
            if (requestLoopRunning)
                return;
            requestLoopRunning = true;
            // Never instant. Makes it more consistent. Though the argument for having instant instantiation
            // of an entity being better for the user can be made so this may change.
            SendCustomEventDelayedFrames(nameof(RequestLoop), 1);
        }

        private bool requestLoopRunning = false;
        public void RequestLoop()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPooling  RequestLoop");
#endif
            object[] request = ArrQueue.Dequeue(ref requestQueue, ref rqStartIndex, ref rqCount);
            ProcessRequest(request);
            if (rqCount == 0)
            {
                requestLoopRunning = false;
                return;
            }
            SendCustomEventDelayedFrames(nameof(RequestLoop), 1);
        }

        private void ProcessRequest(object[] request)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityPooling  ProcessRequest");
#endif
            EntityData entityData = (EntityData)request[0];
            if (entityData.entityIsDestroyed)
                return; // TODO: probably check for the next few requests to make it process faster
            EntityPrototype prototype = entityData.entityPrototype;
            DataList pooled = pooledEntities[prototype.Id].DataList;
            int pooledCount = pooled.Count;
            Entity entity;
            if (pooledCount != 0)
            {
                entity = (Entity)pooled[pooledCount - 1].Reference;
                pooled.RemoveAt(pooledCount - 1);
            }
            else
            {
                GameObject entityGo = Instantiate(prototype.EntityPrefab);
                entity = entityGo.GetComponent<Entity>();
                entity.OnInstantiate(lockstep, entitySystem, wannaBeClasses, prototype, isDefaultInstance: false);
            }
            Transform t = entity.transform;
            t.position = entityData.LastKnownPosition;
            t.rotation = entityData.LastKnownRotation;
            t.localScale = entityData.LastKnownScale;
            entity.gameObject.SetActive(true);
            entity.AssociateWithEntityData(entityData);
        }
    }
}
