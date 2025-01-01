
namespace JanSharp
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class EntityExtensionInputActionAttribute : System.Attribute
    {
        public EntityExtensionInputActionAttribute()
        { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class AssociatedEntityExtensionDataAttribute : System.Attribute
    {
        readonly System.Type associatedDataType;
        public System.Type AssociatedDataType => associatedDataType;

        public AssociatedEntityExtensionDataAttribute(System.Type associatedDataType)
        {
            this.associatedDataType = associatedDataType;
        }
    }
}
