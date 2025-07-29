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
        [Header("Important: Changing this will cause EntityPrototypes in scenes\n"
            + "referencing this prototype definition to lose that reference.")]
        public GameObject entityPrefab;
        public Vector3 defaultScale;
        // public Entity defaultEntityInst;
        // TODO: this probably needs a "highest id" field just like build time id assignment does
        public uint[] localExtensionIds;
        public string[] extensionDataClassNames;
    }
}
