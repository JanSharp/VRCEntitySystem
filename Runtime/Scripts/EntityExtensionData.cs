using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public abstract class EntityExtensionData : SerializableWannaBeClass
    {
        [HideInInspector][SingletonReference] public EntitySystem entitySystem;
        [System.NonSerialized] public int extensionIndex;
        [System.NonSerialized] public EntityData entityData;
        [System.NonSerialized] public Entity entity;
        [System.NonSerialized] public EntityExtension extension;

        public EntityExtensionData WannaBeConstructor(int extensionIndex, EntityData entityData)
        {
            this.extensionIndex = extensionIndex;
            this.entityData = entityData;
            entityData.allExtensionData[extensionIndex] = this;
            // entity = null; // Default.
            // extension = null; // Default.
            return this;
        }

        public void SetEntityAndExtension(Entity entity, EntityExtension extension)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityExtensionData  SetEntityAndExtension");
#endif
            this.entity = entity;
            this.extension = extension;
            extension.entityData = entityData;
            extension.extensionData = this;
        }

        /// <summary>
        /// <para>Used when creating a new entity. This can be latency hidden, thus this function gets called
        /// in a potentially latency hidden context, making it not actually game state safe.</para>
        /// <para>There could be unknown code up the call stack, thus the value of
        /// <see cref="LockstepAPI.InGameStateSafeEvent"/> cannot be trusted.</para>
        /// <para>The <see cref="OnEntityExtensionDataCreated"/> event gets raised in a game state safe manner
        /// after <see cref="InitFromDefault(EntityExtension)"/> has run.</para>
        /// <para>It is semantically incorrect to call <see cref="InitFromPreInstantiated(EntityExtension)"/>
        /// or <see cref="InitBeforeDeserialization"/> from within
        /// <see cref="InitFromDefault(EntityExtension)"/>, as both of the events mentioned are guaranteed to
        /// be game state safe while <see cref="InitFromDefault(EntityExtension)"/> is not.</para>
        /// <para><see cref="InitFromDefault(EntityExtension)"/> is only allowed to modify this specific
        /// entity data and all its entity extension data, while explicitly being disallowed to modify any
        /// game state external to this entity.</para>
        /// <para>The given <paramref name="entityExtension"/> instance is immutable.</para>
        /// </summary>
        /// <param name="entityExtension">This extension instance is immutable.</param>
        public abstract void InitFromDefault(EntityExtension entityExtension);
        /// <summary>
        /// <para>Game state safe. (Happens inside of <see cref="LockstepEventType.OnInit"/>)</para>
        /// </summary>
        /// <param name="entityExtension"></param>
        public abstract void InitFromPreInstantiated(EntityExtension entityExtension);
        /// <summary>
        /// <para>Game state safe. (Happens inside of game state deserialization.)</para>
        /// </summary>
        public abstract void InitBeforeDeserialization();
        /// <summary>
        /// <para>The game state safe variant of <see cref="InitFromDefault(EntityExtension)"/>.</para>
        /// <para>Happens inside of an input action.</para>
        /// <para>Use this to promote any state contained within this entity data and all its entity
        /// extension data that was initialized in <see cref="InitFromDefault(EntityExtension)"/> to the
        /// overall game state.</para>
        /// <para>In other words this event is allowed and should modify any game state external to this
        /// entity matching what said external state should be due to this entity existing. Just like how
        /// <see cref="InitFromPreInstantiated(EntityExtension)"/> or <see cref="InitBeforeDeserialization"/>
        /// may have modified external game states, but <see cref="InitFromDefault(EntityExtension)"/>
        /// couldn't as it is not allowed to due to being potentially latency hidden.</para>
        /// <para>If this event modifies this entity data or any of its entity extension data, it must call
        /// <see cref="EntityData.ResetLatencyStateIfItDiverged"/>. This may have undesired effects to the
        /// user (such as rubber banding for example) so it is likely best avoided.</para>
        /// </summary>
        public virtual void OnEntityExtensionDataCreated() { }
        /// <summary>
        /// <para>Not game state safe.</para>
        /// </summary>
        public virtual void OnAssociatedWithExtension() { }
        /// <summary>
        /// <para>Not game state safe.</para>
        /// </summary>
        public virtual void OnDisassociateFromExtension() { }
        /// <summary>
        /// <para>Game state safe.</para>
        /// </summary>
        public virtual void OnEntityExtensionDataDestroyed() { }
        /// <summary>
        /// <para>Game state safe.</para>
        /// </summary>
        public virtual void ImportedWithoutDeserialization() { } // TODO: maybe add On prefix, also add to lifecycle

        public ulong SendExtensionDataInputAction(string methodName) => entitySystem.SendExtensionDataInputAction(this, methodName);
    }

    public static class EntityExtensionDataStatics
    {
        public static EntityExtensionData New(
            WannaBeClassesManager wannaBeClasses,
            string extensionDataClassName,
            int extensionIndex,
            EntityData entityData)
        {
#if ENTITY_SYSTEM_DEBUG
            Debug.Log($"[EntitySystemDebug] EntityExtensionDataStatics  New - extensionDataClassName: {extensionDataClassName}, extensionIndex: {extensionIndex}");
#endif
            return ((EntityExtensionData)wannaBeClasses.NewDynamic(extensionDataClassName))
                .WannaBeConstructor(extensionIndex, entityData);
        }
    }
}
