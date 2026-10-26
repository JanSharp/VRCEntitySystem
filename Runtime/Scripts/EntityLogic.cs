using UdonSharp;
using UnityEngine;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("d627f7fa95da90f1f87280f822155c9d")] // Runtime/Prefabs/EntitySystem.prefab
    public class EntityLogic : UdonSharpBehaviour
    {
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
            if (!entity.noTransformSync && !entityData.noTransformSync)
            {
                Transform t = entity.transform;
                interpolation.CancelPositionInterpolation(t);
                interpolation.CancelRotationInterpolation(t);
                interpolation.CancelScaleInterpolation(t);
                t.position = entityData.position;
                t.rotation = entityData.rotation;
                t.localScale = entityData.scale;
            }
            // TODO: what to do about hidden?
            // TODO: handle parent entity
            // TODO: handle child entities
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
            // TODO: this probably needs to do more
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

            if (t.position == position)
                interpolation.CancelPositionInterpolation(t);
            else if (doInterpolate)
                interpolation.LerpWorldPosition(t, position, interpolationDuration);
            else
            {
                interpolation.CancelPositionInterpolation(t);
                t.position = position;
            }

            if (t.rotation == rotation)
                interpolation.CancelRotationInterpolation(t);
            else if (doInterpolate)
                interpolation.LerpWorldRotation(t, rotation, interpolationDuration);
            else
            {
                interpolation.CancelRotationInterpolation(t);
                t.rotation = rotation;
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
