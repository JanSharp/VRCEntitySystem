using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntitySystem : LockstepGameState
    {
        [SingletonReference] [HideInInspector] [SerializeField] private LockstepAPI lockstep;
        [SingletonReference] [HideInInspector] [SerializeField] private WannaBeClassesManager wannaBeClasses;
        public EntityPrototype[] entityPrototypes;
        private DataDictionary entityPrototypesById = new DataDictionary();
        private DataDictionary entityPrototypesByName = new DataDictionary();
        public Entity[] preInstantiatedEntityInstances;
        public uint[] preInstantiatedEntityInstanceIds;
        public EntityPrototype[] preInstantiatedEntityInstancePrototypes;
        private Entity[] entityInstances = new Entity[ArrList.MinCapacity];
        private int entityInstancesCount = 0;
        private uint nextEntityId = 1u;

        public override string GameStateInternalName => "jansharp.entity-system";
        public override string GameStateDisplayName => "Entity System";
        public override bool GameStateSupportsImportExport => false;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;

        void Start()
        {
            InitEntityPrototypes();
            InitPreInstantiatedEntities();
        }

        private void InitEntityPrototypes()
        {
            foreach (EntityPrototype prototype in entityPrototypes)
            {
                entityPrototypesById.Add(prototype.Id, prototype);
                entityPrototypesByName.Add(prototype.PrototypeName, prototype);
            }
        }

        private void InitPreInstantiatedEntities()
        {
            int length = preInstantiatedEntityInstances.Length;
            ArrList.EnsureCapacity(ref entityInstances, length);
            for (int i = 0; i < length; i++)
            {
                Entity entity = preInstantiatedEntityInstances[i];
                if (entity == null)
                    continue;
                EntityPrototype prototype = preInstantiatedEntityInstancePrototypes[i];
                entity.prototype = prototype;
                EntityData entityData = NewEntityData();
                entity.entityData = entityData;
                entityData.InitFromEntity(entity);
                entityData.id = preInstantiatedEntityInstanceIds[i];
                ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
            }
        }

        private void InitNextEntityId()
        {
            nextEntityId = preInstantiatedEntityInstanceIds.Length == 0
                ? 1u
                : (preInstantiatedEntityInstanceIds[preInstantiatedEntityInstanceIds.Length - 1] + 1u);
        }

        private EntityData NewEntityData()
        {
            return wannaBeClasses.New<EntityData>(nameof(EntityData));
        }

        private EntityPrototype GetEntityPrototype(uint prototypeId) => (EntityPrototype)entityPrototypesById[prototypeId].Reference;
        private bool TryGetEntityPrototype(uint prototypeId, out EntityPrototype entityPrototype)
        {
            if (entityPrototypesById.TryGetValue(prototypeId, out DataToken token))
            {
                entityPrototype = (EntityPrototype)token.Reference;
                return true;
            }
            entityPrototype = null;
            return false;
        }

        private EntityPrototype GetEntityPrototype(string prototypeName) => (EntityPrototype)entityPrototypesByName[prototypeName].Reference;
        private bool TryGetEntityPrototype(string prototypeName, out EntityPrototype entityPrototype)
        {
            if (entityPrototypesByName.TryGetValue(prototypeName, out DataToken token))
            {
                entityPrototype = (EntityPrototype)token.Reference;
                return true;
            }
            entityPrototype = null;
            return false;
        }

        public void SendCreateEntityIA(uint prototypeId)
        {
            lockstep.WriteSmallUInt(prototypeId);
            lockstep.SendInputAction(createEntityIAId);
        }

        [HideInInspector] [SerializeField] private uint createEntityIAId;
        [LockstepInputAction(nameof(createEntityIAId))]
        public void OnCreateEntityIA()
        {
            uint prototypeId = lockstep.ReadSmallUInt();
            CreateEntity(prototypeId);
        }

        public Entity CreateEntity(uint prototypeId)
        {
            if (!TryGetEntityPrototype(prototypeId, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype);
        }

        public Entity CreateEntity(string prototypeName)
        {
            if (!TryGetEntityPrototype(prototypeName, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype);
        }

        public Entity CreateEntity(EntityPrototype prototype)
        {
            GameObject entityGo = Instantiate(prototype.EntityPrefab);
            Entity entity = entityGo.GetComponent<Entity>();
            EntityData entityData = wannaBeClasses.RegisterManuallyInstantiated(entity.entityData);
            entityData.id = nextEntityId++;
            ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
            return entity;
        }

        public override void SerializeGameState(bool isExport)
        {
            lockstep.WriteSmallUInt(nextEntityId);

            lockstep.WriteSmallUInt((uint)entityInstancesCount);
            for (int i = 0; i < entityInstancesCount; i++)
            {
                Entity entity = entityInstances[i];
                entity.entityData.Serialize(isExport);
                // lockstep.WriteCustomClass(entity.entityData);
            }
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion)
        {
            nextEntityId = lockstep.ReadSmallUInt();

            entityInstancesCount = (int)lockstep.ReadSmallUInt();
            ArrList.EnsureCapacity(ref entityInstances, entityInstancesCount);
            for (int i = 0; i < entityInstancesCount; i++)
            {
                // EntityData
            }

            return null;
        }
    }
}
