
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
}
