using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EntityData : WannaBeClass
    {
        [HideInInspector][SingletonReference] public LockstepAPI lockstep;
        [HideInInspector][SingletonReference] public EntitySystem entitySystem;
        [HideInInspector][SingletonReference] public InterpolationManager interpolation;
        /// <summary>
        /// <para>Negative 1 means that the entity has been destroyed.</para>
        /// <para>Not part of the public api.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        [System.NonSerialized] public int instanceIndex;
        [System.NonSerialized] public EntityPrototype entityPrototype;
        /// <summary>
        /// <para>Not game state safe.</para>
        /// </summary>
        [System.NonSerialized] public Entity entity;
        /// <summary>
        /// <para>Once <see langword="true"/>, <see langword="true"/> forever.</para>
        /// <para>Not game state safe.</para>
        /// </summary>
        [System.NonSerialized] public bool entityIsDestroyed;
        [System.NonSerialized] public bool wasPreInstantiated = false;
        [System.NonSerialized] public ulong uniqueId;
        [System.NonSerialized] public uint id;
        private bool noPositionSync;
        private bool noRotationSync;
        private bool noScaleSync;
        [System.NonSerialized] public Vector3 position;
        [System.NonSerialized] public Quaternion rotation;
        [System.NonSerialized] public Vector3 scale;
        [System.NonSerialized] public uint createdByPlayerId;
        [System.NonSerialized] public uint lastUserPlayerId;
        [System.NonSerialized] public bool hidden;
        [System.NonSerialized] public EntityData parentEntity;
        [System.NonSerialized] public EntityData[] childEntities = new EntityData[0];
        /*[HideInInspector]*/
        public EntityExtensionData[] allExtensionData;

        [System.NonSerialized] public uint unresolvedParentEntityId;
        [System.NonSerialized] public uint[] unresolvedChildEntitiesIds;

        private bool IsDummyEntityDataForImport => entityPrototype == null;
        [System.NonSerialized] public EntityPrototypeMetadata importedMetadata;

        private uint localPlayerId;

        public bool NoPositionSync
        {
            get => noPositionSync;
            set
            {
                noPositionSync = value;
                if (value)
                    position = Vector3.zero;
            }
        }
        public bool NoRotationSync
        {
            get => noRotationSync;
            set
            {
                noRotationSync = value;
                if (value)
                    rotation = Quaternion.identity;
            }
        }
        public bool NoScaleSync
        {
            get => noScaleSync;
            set
            {
                noScaleSync = value;
                if (value)
                    scale = Vector3.one;
            }
        }

        public EntityData WannaBeConstructor(EntityPrototype entityPrototype, ulong uniqueId, uint id)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  WannaBeConstructor - entityPrototype.PrototypeName: {(entityPrototype == null ? "<null>" : entityPrototype.PrototypeName)}, uniqueId: 0x{uniqueId:x16}, id: {id}");
#endif
            this.entityPrototype = entityPrototype;
            this.uniqueId = uniqueId;
            this.id = id;
            localPlayerId = (uint)Networking.LocalPlayer.playerId;

            if (IsDummyEntityDataForImport)
                return this;

            string[] extensionClassNames = entityPrototype.ExtensionDataClassNames;
            int length = extensionClassNames.Length;
            if (allExtensionData.Length == length) // Already populated for pre instantiated EntityData.
                for (int i = 0; i < length; i++)
                    allExtensionData[i].WannaBeConstructor(i, this);
            else
            {
                allExtensionData = new EntityExtensionData[length];
                for (int i = 0; i < length; i++)
                    allExtensionData[i] = EntityExtensionDataStatics.New(WannaBeClasses, extensionClassNames[i], i, this);
            }

            return this;
        }

        public override void WannaBeDestructor()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  WannaBeDestructor");
#endif
            if (allExtensionData == null)
                return;
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.DecrementRefsCount();
        }

        public void SendDestroyEntityIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SendDestroyEntityIA");
#endif
            entitySystem.SendDestroyEntityIA(this);
        }

        public void SetEntity(Entity entity)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SetEntity");
#endif
            this.entity = entity;
            entity.entityData = this;
        }

        public void InitFromDefault(Vector3 position, Quaternion rotation, Vector3 scale)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromDefault");
#endif
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            createdByPlayerId = 0u;
            lastUserPlayerId = 0u;
            hidden = false;
            parentEntity = null;
            InitAllExtensionDataFromDefault();
        }

        private void InitAllExtensionDataFromDefault()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitAllExtensionDataFromDefault");
#endif
            int length = allExtensionData.Length;
            EntityExtension[] defaultExtensions = entityPrototype.DefaultEntityInst.extensions;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromDefault(defaultExtensions[i]);
        }

        internal void InitFromPreInstantiated(Entity entity)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitFromPreInstantiated");
#endif
            Transform t = entity.transform;
            position = t.position;
            rotation = t.rotation;
            scale = t.localScale;
            createdByPlayerId = 0u;
            lastUserPlayerId = 0u;
            hidden = false;
            parentEntity = null;
            InitAllExtensionDataFromPreInstantiated(entity.extensions);
        }

        private void InitAllExtensionDataFromPreInstantiated(EntityExtension[] extensions)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  InitAllExtensionDataFromPreInstantiated");
#endif
            int length = allExtensionData.Length;
            for (int i = 0; i < length; i++)
                allExtensionData[i].InitFromPreInstantiated(extensions[i]);
        }

        public void OnEntityDataCreated()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnEntityDataCreated");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnEntityExtensionDataCreated();
        }

        public void OnAssociatedWithEntity()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnAssociatedWithEntity");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnAssociatedWithExtension();
        }

        public void OnDisassociateFromEntity()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnDisassociateFromEntity");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnDisassociateFromExtension();
        }

        public void OnEntityDataDestroyed()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  OnEntityDataDestroyed");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                extensionData.OnEntityExtensionDataDestroyed();
        }

        private void SerializeTransformValues()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  SerializeTransformValues");
#endif
            if (!noPositionSync)
                lockstep.WriteVector3(position);
            if (!noRotationSync)
                lockstep.WriteQuaternion(rotation);
            if (!noScaleSync)
                lockstep.WriteVector3(scale);
        }

        private void DeserializeTransformValue()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeTransformValue");
#endif
            position = noPositionSync ? Vector3.zero : lockstep.ReadVector3();
            rotation = noRotationSync ? Quaternion.identity : lockstep.ReadQuaternion();
            scale = noScaleSync ? Vector3.one : lockstep.ReadVector3();
        }

        public void Serialize(bool isExport)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Serialize");
#endif
            lockstep.WriteFlags(noPositionSync, noRotationSync, noScaleSync, hidden);
            SerializeTransformValues();
            lockstep.WriteSmallUInt(createdByPlayerId);
            lockstep.WriteSmallUInt(lastUserPlayerId);
            lockstep.WriteSmallUInt(parentEntity == null ? 0u : parentEntity.id);
            lockstep.WriteSmallUInt((uint)childEntities.Length);
            foreach (EntityData child in childEntities)
                lockstep.WriteSmallUInt(child.id);
            if (isExport)
                lockstep.WriteSmallUInt((uint)allExtensionData.Length);
            foreach (EntityExtensionData extensionData in allExtensionData)
                lockstep.WriteCustomNullableClass(extensionData); // TODO: why nullable? Probably because of exports, but even for non exports?
        }

        public void Deserialize(bool isImport, uint importedDataVersion)
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  Deserialize");
#endif
            lockstep.ReadFlags(out noPositionSync, out noRotationSync, out noScaleSync, out hidden);
            DeserializeTransformValue();
            createdByPlayerId = lockstep.ReadSmallUInt();
            lastUserPlayerId = lockstep.ReadSmallUInt();
            unresolvedParentEntityId = lockstep.ReadSmallUInt();
            int childEntitiesLength = (int)lockstep.ReadSmallUInt();
            unresolvedChildEntitiesIds = new uint[childEntitiesLength];
            for (int i = 0; i < childEntitiesLength; i++)
                unresolvedChildEntitiesIds[i] = lockstep.ReadSmallUInt();
            if (isImport)
            {
                ResolveImportedParentEntityId();
                ResolveImportedChildEntityIds();
            }
            if (isImport)
                ImportAllExtensionData();
            else
                DeserializeAllExtensionData();
        }

        private void ResolveImportedParentEntityId()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  ResolveImportedParentEntityId");
#endif
            entitySystem.TryGetRemappedImportedEntityData(unresolvedParentEntityId, out parentEntity);
            unresolvedParentEntityId = 0u;
        }

        private void ResolveImportedChildEntityIds()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  ResolveImportedChildEntityIds");
#endif
            EntityData[] childEntities = new EntityData[unresolvedChildEntitiesIds.Length];
            int i = 0;
            foreach (uint childId in unresolvedChildEntitiesIds)
                if (entitySystem.TryGetRemappedImportedEntityData(childId, out EntityData data))
                    childEntities[i++] = data;
            if (i != childEntities.Length)
            {
                EntityData[] shortenedList = new EntityData[i];
                System.Array.Copy(childEntities, shortenedList, i);
                childEntities = shortenedList;
            }
            unresolvedChildEntitiesIds = null;
        }

        private void DeserializeAllExtensionData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  DeserializeAllExtensionData");
#endif
            foreach (EntityExtensionData extensionData in allExtensionData)
                lockstep.ReadCustomNullableClass(extensionData);
        }

        private void ImportAllExtensionData()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] EntityData  ImportAllExtensionData");
#endif
            int length = (int)lockstep.ReadSmallUInt();
            if (IsDummyEntityDataForImport)
            {
                for (int i = 0; i < length; i++)
                    lockstep.SkipCustomClass(out var discard1, out var discord2); // Cannot use 'out _'.
                return;
            }
            for (int i = 0; i < length; i++)
            {
                string newExtensionClassName = importedMetadata.resolvedExtensionClassNames[i];
                if (newExtensionClassName == null)
                {
                    lockstep.SkipCustomClass(out var discard1, out var discord2); // Cannot use 'out _'.
                    continue;
                }
                int index = importedMetadata.resolvedExtensionIndexes[i];
                EntityExtensionData extensionData = allExtensionData[index];
                if (!lockstep.ReadCustomNullableClass(extensionData))
                    extensionData.ImportedWithoutDeserialization();
            }
        }

        /// <summary>
        /// <para>Send function is here <see cref="Entity.SendTransformChangeIA"/>.</para>
        /// </summary>
        public void OnTransformChangeIA()
        {
#if EntitySystemDebug
            Debug.Log($"[EntitySystemDebug] Entity  OnTransformChangeIA");
#endif
            lockstep.ReadFlags(
                out bool positionChange, out bool discontinuousPositionChange,
                out bool rotationChange, out bool discontinuousRotationChange,
                out bool scaleChange, out bool discontinuousScaleChange);

            if (positionChange)
                position = lockstep.ReadVector3();
            if (rotationChange)
                rotation = lockstep.ReadQuaternion();
            if (scaleChange)
                scale = lockstep.ReadVector3();

            if (entity == null || lockstep.SendingPlayerId == localPlayerId)
                return;

            Transform entityTransform = entity.transform;

            if (positionChange && !noPositionSync)
            {
                if (discontinuousPositionChange)
                    interpolation.InterpolateWorldPosition(entityTransform, position, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelWorldPositionInterpolation(entityTransform);
                    entityTransform.position = position;
                }
            }

            if (rotationChange && !noRotationSync)
            {
                if (discontinuousRotationChange)
                    interpolation.InterpolateWorldRotation(entityTransform, rotation, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelRotationInterpolation(entityTransform);
                    entityTransform.rotation = rotation;
                }
            }

            if (scaleChange && !noScaleSync)
            {
                if (!discontinuousScaleChange)
                    interpolation.InterpolateLocalScale(entityTransform, scale, Entity.TransformChangeInterpolationDuration);
                else
                {
                    interpolation.CancelLocalScaleInterpolation(entityTransform);
                    entityTransform.localScale = scale;
                }
            }
        }
    }
}
