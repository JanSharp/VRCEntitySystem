using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    public class EntityPooling : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private EntitySystem entitySystem;
        [HideInInspector][SerializeField][SingletonReference] private EntityDataLogic entityDataLogic;
        [HideInInspector][SerializeField][SingletonReference] private EntityLogic entityLogic;
        [HideInInspector][SerializeField][SingletonReference] private UpdateManager updateManager;
        /// <summary>Required by the <see cref="UpdateManager"/>.</summary>
        [System.NonSerialized] public int customUpdateInternalIndex;

        /// <summary>
        /// <para>The amount of requests to check, looking for a not already destroyed nor already created
        /// entity to create.</para>
        /// </summary>
        private const int MaxRequestsToCheckPerFrame = 10;

        private EntityData[] requestQueue = new EntityData[ArrQueue.MinCapacity];
        private int rqStartIndex = 0;
        private int rqCount = 0;

        ///cSpell:ignore rwcq

        private object[][] requestsWithCallbackQueue = new object[ArrQueue.MinCapacity][];
        private int rwcqStartIndex = 0;
        private int rwcqCount = 0;

        public bool isInTheMiddleOfCreatingAnEntity = false;

        private DataDictionary pooledEntities = new DataDictionary();

        private void Start()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  Start");
#endif
            foreach (EntityPrototype prototype in entitySystem.EntityPrototypes)
                pooledEntities.Add(prototype.Id, new DataList());

            // Could start gradual deletion loop of stale pooled entities.
            // Could use a linked list of all pooled entities, unfortunately would have to be bidirectional.
            // However the creation of entities is so expensive due to Udon script initialization taking
            // multiple milliseconds that gradually deleting pooled entities is not worth the small bit of
            // memory that would be regained. Unless it'd be like hundreds or thousands of unused pooled
            // objects, then maybe.
        }

        public void RequestEntity(EntityData entityData, bool highPriority = false)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  RequestEntity");
#endif
            entityData.IncrementRefsCount();
            if (highPriority)
                ArrQueue.EnqueueAtFront(ref requestQueue, ref rqStartIndex, ref rqCount, entityData);
            else
                ArrQueue.Enqueue(ref requestQueue, ref rqStartIndex, ref rqCount, entityData);
            // Never instantly process a request. Makes it more consistent. Though the argument for having
            // instant instantiation of an entity being better for the user can be made so this may change.
            StartRequestLoop();
        }

        public void ReturnEntity(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  ReturnEntity - entityData");
#endif
            entityData.entityIsDestroyed = true;
            Entity entity = entityData.entity;
            if (entity == null)
                return;
            ReturnEntity(entity);
            entityDataLogic.OnDisassociateFromEntity(entityData);
            entityData.entity = null;
            entity.entityData = null;
        }

        public void ReturnEntity(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  ReturnEntity - entity");
#endif
            entity.gameObject.SetActive(false);
            entityLogic.DisassociateFromEntityDataAndReset(entity, entity.prototype.DefaultEntityInst);
            pooledEntities[entity.prototype.Id].DataList.Add(entity);
        }

        private void StartRequestLoop()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  StartRequestLoop");
#endif
            updateManager.Register(this);
        }

        public void CustomUpdate()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  CustomUpdate");
#endif
            for (int i = 0; i < MaxRequestsToCheckPerFrame; i++)
            {
                EntityData entityData = ArrQueue.Dequeue(ref requestQueue, ref rqStartIndex, ref rqCount);
                if (!IsMissingEntity(entityData))
                {
                    entityData.DecrementRefsCount();
                    continue;
                }
                CreateEntityAvoidingRecursion(entityData, null, null, null);
                entityData.DecrementRefsCount();
                if (rqCount == 0)
                    updateManager.Deregister(this);
                break;
            }
        }

        public bool IsMissingEntity(EntityData entityData)
        {
            return !entityData.entityIsDestroyed && entityData.entity == null;
        }

        public void CreateEntityAvoidingRecursion(
            EntityData entityData,
            UdonSharpBehaviour callbackInst,
            string callbackEventName,
            object callbackCustomData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  CreateEntityAvoidingRecursion");
#endif
            if (isInTheMiddleOfCreatingAnEntity)
            {
                entityData.IncrementRefsCount();
                ArrQueue.Enqueue(ref requestsWithCallbackQueue, ref rwcqStartIndex, ref rwcqCount, new object[]
                {
                    entityData,
                    callbackInst,
                    callbackEventName,
                    callbackCustomData,
                });
                return;
            }

            // Must use locals since parameters would be overwritten when CreateEntityAvoidingRecursion
            // gets called in the middle of entity creation.
            EntityData localEntityData = entityData;
            UdonSharpBehaviour localCallbackInst = callbackInst;
            string localCallbackEventName = callbackEventName;
            object localCallbackCustomData = callbackCustomData;

            localEntityData.IncrementRefsCount();
            CreateEntity(localEntityData);
            if (localCallbackInst != null)
                entitySystem.RaiseEnsureEntityExistsCallback(localEntityData, localCallbackInst, localCallbackEventName, localCallbackCustomData);
            localEntityData.DecrementRefsCount();

            while (rwcqCount != 0)
            {
                object[] request = ArrQueue.Dequeue(ref requestsWithCallbackQueue, ref rwcqStartIndex, ref rwcqCount);
                localEntityData = (EntityData)request[0];
                localCallbackInst = (UdonSharpBehaviour)request[1];
                localCallbackEventName = (string)request[2];
                localCallbackCustomData = request[3];

                CreateEntity(localEntityData);
                if (localCallbackInst != null)
                    entitySystem.RaiseEnsureEntityExistsCallback(localEntityData, localCallbackInst, localCallbackEventName, localCallbackCustomData);
                localEntityData.DecrementRefsCount();
            }
        }

        private void CreateEntity(EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityPooling  CreateEntity");
#endif
            isInTheMiddleOfCreatingAnEntity = true;
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
#if ENTITY_SYSTEM_STOPWATCH
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
#endif
                GameObject entityGo = Instantiate(prototype.EntityPrefabInst);
#if ENTITY_SYSTEM_STOPWATCH
                sw.Stop();
                Debug.Log($"[EntitySystemDebug] [sw] EntityPooling  CreateEntity (inner) - instantiateMs: {sw.Elapsed.TotalMilliseconds:f3}");
#endif
                entity = entityGo.GetComponent<Entity>();
                entityLogic.OnInstantiate(entity, prototype, isDefaultInstance: false);
            }
            Transform t = entity.transform;
            t.position = entityData.LastKnownPosition;
            t.rotation = entityData.LastKnownRotation;
            t.localScale = entityData.LastKnownScale;
            entity.gameObject.SetActive(true);
            entityLogic.AssociateWithEntityData(entity, entityData);
            isInTheMiddleOfCreatingAnEntity = false;
        }
    }
}
