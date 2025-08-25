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
        /// <summary>
        /// <para>Used by editor scripting to prevent ids getting reused when an extension got removed and
        /// another got added.</para>
        /// <para>Id <c>0u</c> is invalid. It must be invalid, in fact, because the default value for
        /// <see cref="highestExtensionId"/> is <c>0u</c>, which tells the system that this is the highest
        /// id that must not be reused - even though there is nothing using it.</para>
        /// <para>But this is good anyway, having a simple way to declare an id being invalid is
        /// useful.</para>
        /// </summary>
        public uint highestExtensionId;
        public uint[] localExtensionIds;
        public string[] extensionDataClassNames; // TODO: Could probably change this to a different type now.
    }
}
