using UnityEngine;

namespace JanSharp
{
    public class EntityPrototypeDependency : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
        [Tooltip("Prototypes in this list will get added to the scene automatically "
            + "upon entering play mode or building the world.")]
        public EntityPrototypeDefinition[] prototypes;
    }
}
