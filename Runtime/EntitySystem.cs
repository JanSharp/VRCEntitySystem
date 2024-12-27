using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript]
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
        private DataDictionary entityInstancesById = new DataDictionary();
        private int entityInstancesCount = 0;
        private uint nextEntityId = 1u;

        public override string GameStateInternalName => "jansharp.entity-system";
        public override string GameStateDisplayName => "Entity System";
        public override bool GameStateSupportsImportExport => false;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;

        private void Start()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  Start");
            #endif
            InitEntityPrototypes();
            InitPreInstantiatedEntities();
        }

        private void InitEntityPrototypes()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitEntityPrototypes");
            #endif
            foreach (EntityPrototype prototype in entityPrototypes)
            {
                entityPrototypesById.Add(prototype.Id, prototype);
                entityPrototypesByName.Add(prototype.PrototypeName, prototype);
            }
        }

        private void InitPreInstantiatedEntities()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitPreInstantiatedEntities");
            #endif
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
                // TODO: handle extensions in many ways
                ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
                entityInstancesById.Add(entityData.id, entity);
            }
        }

        private void InitNextEntityId()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InitNextEntityId");
            #endif
            nextEntityId = preInstantiatedEntityInstanceIds.Length == 0
                ? 1u
                : (preInstantiatedEntityInstanceIds[preInstantiatedEntityInstanceIds.Length - 1] + 1u);
        }

        private EntityData NewEntityData()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  NewEntityData");
            #endif
            return wannaBeClasses.New<EntityData>(nameof(EntityData));
        }

        public EntityPrototype GetEntityPrototype(uint prototypeId) => (EntityPrototype)entityPrototypesById[prototypeId].Reference;
        public bool TryGetEntityPrototype(uint prototypeId, out EntityPrototype entityPrototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetEntityPrototype");
            #endif
            if (entityPrototypesById.TryGetValue(prototypeId, out DataToken token))
            {
                entityPrototype = (EntityPrototype)token.Reference;
                return true;
            }
            entityPrototype = null;
            return false;
        }

        public EntityPrototype GetEntityPrototype(string prototypeName) => (EntityPrototype)entityPrototypesByName[prototypeName].Reference;
        public bool TryGetEntityPrototype(string prototypeName, out EntityPrototype entityPrototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  TryGetEntityPrototype");
            #endif
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
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendCreateEntityIA");
            #endif
            lockstep.WriteSmallUInt(prototypeId);
            lockstep.SendInputAction(createEntityIAId);
        }

        [HideInInspector] [SerializeField] private uint createEntityIAId;
        [LockstepInputAction(nameof(createEntityIAId))]
        public void OnCreateEntityIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnCreateEntityIA");
            #endif
            uint prototypeId = lockstep.ReadSmallUInt();
            CreateEntity(prototypeId);
        }

        public Entity CreateEntity(uint prototypeId)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            if (!TryGetEntityPrototype(prototypeId, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype);
        }

        public Entity CreateEntity(string prototypeName)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            if (!TryGetEntityPrototype(prototypeName, out EntityPrototype prototype))
                return null;
            return CreateEntity(prototype);
        }

        public Entity CreateEntity(EntityPrototype prototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  CreateEntity");
            #endif
            Entity entity = InstantiateEntity(prototype);
            EntityData entityData = NewEntityData();
            entity.entityData = entityData;
            entityData.InitFromEntity(entity);
            entityData.id = nextEntityId++;
            string[] extensionClassNames = prototype.ExtensionClassNames;
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            for (int i = 0; i < extensionClassNames.Length; i++)
            {
                EntityExtension extension = entity.extensions[i];
                extension.lockstep = lockstep;
                extension.entitySystem = this;
                extension.entity = entity;
                EntityExtensionData extensionData = wannaBeClasses.New<EntityExtensionData>(extensionClassNames[i]);
                allExtensionData[i] = extensionData;
                extension.extensionData = extensionData;
                extensionData.extension = extension;
                extensionData.InitFromExtension();
            }
            ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
            entityInstancesById.Add(entityData.id, entity);
            return entity;
        }

        public void WriteEntityExtensionReference(EntityExtension extension)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  WriteEntityExtensionReference");
            #endif
            lockstep.WriteSmallUInt(extension.entity.entityData.id);
            lockstep.WriteSmallUInt((uint)System.Array.IndexOf(extension.entity.extensions, extension));
        }

        public EntityExtension ReadEntityExtensionReferenceDynamic()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  ReadEntityExtensionReferenceDynamic");
            #endif
            uint entityId = lockstep.ReadSmallUInt();
            int index = (int)lockstep.ReadSmallUInt();
            if (!entityInstancesById.TryGetValue(entityId, out DataToken entityToken))
                return null;
            return ((Entity)entityToken.Reference).extensions[index];
        }

        public ulong SendExtensionInputAction(EntityExtension extension, string methodName)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SendExtensionInputAction");
            #endif
            byte[] buffer = new byte[10 + (5 + methodName.Length)]; // No multi byte characters, so this is fine.
            int bufferSize = 0;
            DataStream.WriteSmall(ref buffer, ref bufferSize, (uint)extension.entity.entityData.id);
            DataStream.WriteSmall(ref buffer, ref bufferSize, (uint)System.Array.IndexOf(extension.entity.extensions, extension));
            DataStream.Write(ref buffer, ref bufferSize, methodName); // TODO: use build time generated id instead.
            int iaSize = lockstep.WriteStreamPosition;
            lockstep.ShiftWriteStream(0, bufferSize, iaSize);
            lockstep.WriteStreamPosition = 0;
            lockstep.WriteBytes(buffer, 0, bufferSize);
            lockstep.WriteStreamPosition = bufferSize + iaSize;
            return lockstep.SendInputAction(onExtensionInputActionIAId);
        }

        [HideInInspector] [SerializeField] private uint onExtensionInputActionIAId;
        [LockstepInputAction(nameof(onExtensionInputActionIAId))]
        public void OnExtensionInputActionIA()
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  OnExtensionInputActionIA");
            #endif
            EntityExtension extension = ReadEntityExtensionReferenceDynamic();
            if (extension == null)
                return;
            string methodName = lockstep.ReadString();
            extension.SendCustomEvent(methodName);
        }

        private Entity InstantiateEntity(EntityPrototype prototype)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  InstantiateEntity");
            #endif
            GameObject entityGo = Instantiate(prototype.EntityPrefab);
            Entity entity = entityGo.GetComponent<Entity>();
            entity.lockstep = lockstep;
            entity.entitySystem = this;
            entity.prototype = prototype;
            return entity;
        }

        public override void SerializeGameState(bool isExport)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  SerializeGameState");
            #endif
            lockstep.WriteSmallUInt(nextEntityId);

            lockstep.WriteSmallUInt((uint)entityInstancesCount);
            for (int i = 0; i < entityInstancesCount; i++)
            {
                Entity entity = entityInstances[i];
                lockstep.WriteCustomClass(entity.entityData);
            }
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion)
        {
            #if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntitySystem  DeserializeGameState");
            #endif
            nextEntityId = lockstep.ReadSmallUInt();

            entityInstancesCount = (int)lockstep.ReadSmallUInt();
            ArrList.EnsureCapacity(ref entityInstances, entityInstancesCount);
            for (int i = 0; i < entityInstancesCount; i++)
            {
                EntityData entityData = lockstep.ReadCustomClass<EntityData>(nameof(EntityData));
                // TODO: check for pre instantiated entity id.
                Entity entity = InstantiateEntity(entityData.entityPrototype);
                entity.InitFromEntityData(entityData);
                ArrList.Add(ref entityInstances, ref entityInstancesCount, entity);
                entityInstancesById.Add(entityData.id, entity);
            }

            return null;
        }
    }

    public static class EntitySystemExtension
    {
        public static T ReadEntityExtensionReference<T>(this EntitySystem entitySystem)
            where T : EntityExtension
        {
            return (T)entitySystem.ReadEntityExtensionReferenceDynamic();
        }
    }
}
