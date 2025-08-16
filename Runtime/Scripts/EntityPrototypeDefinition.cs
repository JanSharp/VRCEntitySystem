using UnityEngine;

namespace JanSharp
{
    [CreateAssetMenu(fileName = "Entity", menuName = "Entity Prototype Definition", order = 202)] // Right above Prefab.
    public class EntityPrototypeDefinition : ScriptableObject
    {
        public string prototypeName;
        public string displayName;
        public string shortDescription;
        public string longDescription;
        [Tooltip("Each Prototype Definition used in a scene must use a unique Entity Prefab.\n"
            + "This is required to be able to figure out which prototype pre instantiated prefab instances "
            + "in a scene belong to.")]
        public GameObject entityPrefab;
        public Vector3 defaultScale;
        // public Entity defaultEntityInst;
        // TODO: this probably needs a "highest id" field just like build time id assignment does
        public uint[] localExtensionIds;
        public string[] extensionDataClassNames; // TODO: Could probably change this to a different type now.
    }
}
