
namespace JanSharp
{
    public enum EntitySystemEventType
    {
        OnEntityDeserialized,
        OnEntityCreated,
        OnEntityDestroyed,
    }

    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class EntitySystemEventAttribute : CustomRaisedEventBaseAttribute
    {
        public EntitySystemEventAttribute(EntitySystemEventType eventType)
            : base((int)eventType)
        { }
    }

    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class EntityExtensionDataInputActionAttribute : System.Attribute
    {
        public EntityExtensionDataInputActionAttribute()
        { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class AssociatedEntityExtensionDataAttribute : System.Attribute
    {
        readonly System.Type associatedDataType;
        public System.Type AssociatedDataType => associatedDataType;

        public AssociatedEntityExtensionDataAttribute(System.Type associatedDataType)
        {
            this.associatedDataType = associatedDataType;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class EntityRuntimeEventListenerAttribute : System.Attribute
    {
        readonly string eventName;
        public string EventName => eventName;

        /// <summary>
        /// <para>See
        /// <see cref="Entity.RegisterForRuntimeEvent(UdonSharp.UdonSharpBehaviour, string)"/>.</para>
        /// <para>All this attribute does is validate the method name. The method's name must match the given
        /// <paramref name="eventName"/>. Best used when the <paramref name="eventName"/> is defined through a
        /// reference to a <see langword="const"/> which defines the actual event name, as provided by the
        /// system which will raise the event.</para>
        /// <para>Aside from validation it is also convenient when finding all references to the
        /// aforementioned <see langword="const"/>.</para>
        /// </summary>
        /// <param name="eventName"></param>
        public EntityRuntimeEventListenerAttribute(string eventName)
        {
            this.eventName = eventName;
        }
    }
}
