using UdonSharp;
using UnityEngine;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    public class EntityLogic : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private LockstepAPI lockstep;
        [HideInInspector][SerializeField][SingletonReference] private EntitySystemAPI entitySystem;
        [HideInInspector][SerializeField][SingletonReference] private InterpolationManager interpolation;
        [HideInInspector][SerializeField][SingletonReference] private EntityDataLogic entityDataLogic;

        public void OnInstantiate(
            Entity entity,
            EntityPrototype prototype,
            bool isDefaultInstance)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityLogic  OnInstantiate");
#endif
            entity.prototype = prototype;
            EntityExtension[] extensions = entity.extensions;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
                extensions[i].InternalSetup(i, entity);
            if (isDefaultInstance)
                for (int i = 0; i < length; i++)
                    extensions[i].OnInstantiateDefaultInstance();
            else
                for (int i = 0; i < length; i++)
                    extensions[i].OnInstantiate();
        }

        public void AssociateWithEntityData(Entity entity, EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityLogic  AssociateWithEntityData");
            if (entity.entityData != null)
                Debug.LogError($"[EntitySystemDebug] Attempt to InitFromEntityData an Entity multiple times.");
#endif
            entityDataLogic.SetEntity(entityData, entity);
            ApplyEntityDataWithoutExtensions(entity);
            EntityExtensionData[] allExtensionData = entityData.allExtensionData;
            EntityExtension[] extensions = entity.extensions;
            int length = extensions.Length;
            for (int i = 0; i < length; i++)
                allExtensionData[i].SetEntityAndExtension(entity, extensions[i]);
            for (int i = 0; i < length; i++)
                extensions[i].AssociateWithExtensionData();
            entityDataLogic.OnAssociatedWithEntity(entityData);
        }

        private void ApplyEntityDataWithoutExtensions(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityLogic  ApplyEntityDataWithoutExtensions - entity.noTransformSync: {entity.noTransformSync}, entity.entityData.noTransformSync: {entity.entityData.noTransformSync}");
#endif
            EntityData entityData = entity.entityData;
            EntityData parentEntityData = entityData.parentEntityData;

            // TODO: what to do about hidden?

            // The condition below is game sate unsafe, however the calling context might be game state safe.
            // Ensure any inner logic knows it is unsafe.
            lockstep.OpenGameStateUnsafeScope();

            if (parentEntityData != null && parentEntityData.entity == null)
            {
                // And with this being game state unsafe, from this point until the callback runs,
                // the game state is therefore guaranteed to not get modified.
                entitySystem.EnsureEntityExistsForEntityData(parentEntityData, this, nameof(OnParentEntityCreatedForApplyEntityData), entity);
                return;
            }

            ResumeApplyEntityDataWithoutExtensions(entity);
            lockstep.CloseGameStateUnsafeScope();
        }

        public void OnParentEntityCreatedForApplyEntityData()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityLogic  OnParentEntityCreatedForApplyEntityData");
#endif
            ResumeApplyEntityDataWithoutExtensions((Entity)entitySystem.CallbackCustomData);
            lockstep.CloseGameStateUnsafeScope();
        }

        private void ResumeApplyEntityDataWithoutExtensions(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityLogic  ResumeApplyEntityDataWithoutExtensions");
#endif
            EntityData entityData = entity.entityData;
            EntityData parentEntityData = entityData.parentEntityData;

            Transform t = entity.transform;
            EntityData prevParentEntityData = entity.parentEntityData;
            if (prevParentEntityData != parentEntityData)
            {
                entity.parentEntityData = parentEntityData;

                if (prevParentEntityData != null && prevParentEntityData.entity != null)
                    RemoveFromChildEntities(prevParentEntityData.entity, entityData);

                if (parentEntityData != null && parentEntityData.entity != null)
                {
                    AddToChildEntities(parentEntityData.entity, entityData);
                    t.SetParent(parentEntityData.entity.transform);
                }
                else
                    t.SetParent(null);
            }

            if (!entity.noTransformSync && !entityData.noTransformSync)
            {
                interpolation.CancelPositionInterpolation(t);
                interpolation.CancelRotationInterpolation(t);
                interpolation.CancelScaleInterpolation(t);
                t.localPosition = entityData.position;
                t.localRotation = entityData.rotation;
                t.localScale = entityData.scale;
            }
        }

        private void AddToChildEntities(Entity entity, EntityData childEntityData)
        {
            ArrList.Add(ref entity.childEntities, ref entity.childEntitiesCount, childEntityData);
        }

        private void RemoveFromChildEntities(Entity entity, EntityData childEntityData)
        {
            ArrList.Remove(ref entity.childEntities, ref entity.childEntitiesCount, childEntityData);
        }

        public void ApplyEntityData(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityLogic  ApplyEntityData");
#endif
            ApplyEntityDataWithoutExtensions(entity);
            foreach (EntityExtension extension in entity.extensions)
                extension.ApplyExtensionData();
        }

        public void DisassociateFromEntityDataAndReset(Entity entity, Entity defaultEntityInst)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityLogic  DisassociateFromEntityDataAndReset");
#endif
            ClearParentEntity(entity);
            ClearChildEntities(entity);

            // TODO: what to do about hidden?

            EntityExtension[] defaultExtensions = defaultEntityInst.extensions;
            EntityExtension[] extensions = entity.extensions;
            for (int i = 0; i < extensions.Length; i++)
            {
                EntityExtension extension = extensions[i];
                extension.DisassociateFromExtensionDataAndReset(defaultExtensions[i]);
                extension.entityData = null;
                extension.extensionData = null;
            }
            entity.entityData = null;
        }

        private void ClearParentEntity(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  ClearParentEntity");
#endif
            EntityData parentEntityData = entity.parentEntityData;
            if (parentEntityData == null)
                return;
            if (parentEntityData.entity != null)
                RemoveFromChildEntities(parentEntityData.entity, entity.entityData);
            entity.parentEntityData = null;
            entity.transform.SetParent(null);
        }

        private void ClearChildEntities(Entity entity)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  ClearChildEntities");
#endif
            EntityData[] childEntities = entity.childEntities;
            int childEntitiesCount = entity.childEntitiesCount;
            for (int i = 0; i < childEntitiesCount; i++)
            {
                Entity child = childEntities[i].entity;
                if (child == null)
                    continue;
                child.parentEntityData = null;
                child.transform.SetParent(null);
            }
            entity.childEntitiesCount = 0;
        }

        public void SendSetParentEntityIA(Entity entity, Entity parentEntity, bool doInterpolateRemotely)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  SendSetParentEntityIA");
#endif
            EntityData parentEntityData = parentEntity == null ? null : parentEntity.entityData;
            if (entity.parentEntityData == parentEntityData)
                return;
            Transform t = entity.transform;
            t.SetParent(parentEntity == null ? null : parentEntity.transform);
            // The latency hiding of the function below ends up doing pointless logic because the parent
            // transform and the local values are all correct already, but it ends up setting them again anyway.
            entityDataLogic.SendSetParentEntityIA(
                entity.entityData,
                parentEntityData,
                t.localPosition,
                t.localRotation,
                t.localScale,
                doInterpolateLocally: false,
                doInterpolateRemotely);
        }

        public void SetParentEntityInLS(
            Entity entity,
            EntityData parentEntityData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool doInterpolate)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  SetParentEntityInLS");
#endif
            EntityData entityData = entity.entityData;

            EntityData prevParentEntityData = entity.parentEntityData;
            if (prevParentEntityData != null && prevParentEntityData.entity != null)
                RemoveFromChildEntities(prevParentEntityData.entity, entityData);

            entity.parentEntityData = parentEntityData;

            if (parentEntityData != null && parentEntityData.entity == null)
            {
                entitySystem.EnsureEntityExistsForEntityData(parentEntityData, this, nameof(OnParentEntityCreatedForSetParentEntity), new object[]
                {
                    entity,
                    position,
                    rotation,
                    scale,
                    doInterpolate,
                });
                return;
            }

            ResumeSetParentEntity(entity, position, rotation, scale, doInterpolate);
        }

        public void OnParentEntityCreatedForSetParentEntity()
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  OnParentEntityCreatedForSetParentEntity");
#endif
            object[] callbackData = (object[])entitySystem.CallbackCustomData;
            ResumeSetParentEntity(
                entity: (Entity)callbackData[0],
                position: (Vector3)callbackData[1],
                rotation: (Quaternion)callbackData[2],
                scale: (Vector3)callbackData[3],
                doInterpolate: (bool)callbackData[4]);
        }

        private void ResumeSetParentEntity(
            Entity entity,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool doInterpolate)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  ResumeSetParentEntity");
#endif
            EntityData entityData = entity.entityData;
            EntityData parentEntityData = entity.parentEntityData;
            if (parentEntityData != null && parentEntityData.entity != null)
                AddToChildEntities(parentEntityData.entity, entityData);

            Transform t = entity.transform;

            if (parentEntityData == null)
                t.SetParent(null);
            else
            {
                if (parentEntityData.entity == null)
                {
                    t.SetParent(null);
                    return; // Do not touch the transform of our entity in this case.
                }
                t.SetParent(parentEntityData.entity.transform);
            }

            if (doInterpolate)
            {
                interpolation.LerpLocalPosition(t, position, Entity.SetParentInterpolationDuration);
                interpolation.LerpLocalRotation(t, rotation, Entity.SetParentInterpolationDuration);
                interpolation.LerpLocalScale(t, scale, Entity.SetParentInterpolationDuration);
            }
            else
            {
                interpolation.CancelPositionInterpolation(t);
                interpolation.CancelRotationInterpolation(t);
                interpolation.CancelScaleInterpolation(t);
                t.localPosition = position;
                t.localRotation = rotation;
                t.localScale = scale;
            }
        }

        public void TakeControlOfTransformSync(Entity entity, EntityTransformController controller)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  TakeControlOfTransformSync");
#endif
            if (!entity.noTransformSync)
            {
                entity.noTransformSync = true;
                entity.transformSyncController = controller;
                return;
            }
            EntityTransformController prevController = entity.transformSyncController;
            if (controller == prevController)
                return;
            entity.transformSyncController = controller;
            prevController.OnLatencyControlTakenOver(entity, controller);
        }

        /// <param name="releasingController">Only give back control if this matches the current controller.
        /// Unless the given controller is <see langword="null"/>, then it gives back regardless.</param>
        public void GiveBackControlOfTransformSync(
            Entity entity,
            EntityTransformController releasingController,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float interpolationDuration)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log("[EntitySystemDebug] EntityLogic  GiveBackControlOfTransformSync");
#endif
            if (!entity.noTransformSync || (releasingController != null && entity.transformSyncController != releasingController))
                return;
            entity.noTransformSync = false;
            EntityTransformController prevController = entity.transformSyncController;
            entity.transformSyncController = null;
            if (releasingController == null)
                prevController.OnLatencyControlLost(entity);

            Transform t = entity.transform;

            bool doInterpolate = interpolationDuration > 0f;

            if (t.localPosition == position)
                interpolation.CancelPositionInterpolation(t);
            else if (doInterpolate)
                interpolation.LerpLocalPosition(t, position, interpolationDuration);
            else
            {
                interpolation.CancelPositionInterpolation(t);
                t.localPosition = position;
            }

            if (t.localRotation == rotation)
                interpolation.CancelRotationInterpolation(t);
            else if (doInterpolate)
                interpolation.LerpLocalRotation(t, rotation, interpolationDuration);
            else
            {
                interpolation.CancelRotationInterpolation(t);
                t.localRotation = rotation;
            }

            if (t.localScale == scale)
                interpolation.CancelScaleInterpolation(t);
            else if (doInterpolate)
                interpolation.LerpLocalScale(t, scale, interpolationDuration);
            else
            {
                interpolation.CancelScaleInterpolation(t);
                t.localScale = scale;
            }
        }
    }
}
