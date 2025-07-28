using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class EntitySystemOnBuild
    {
        private static List<EntityPrototype> prototypes;

        static EntitySystemOnBuild()
        {
            OnBuildUtil.RegisterTypeCumulative<EntityPrototype>(OnPrototypesBuild, order: -1);
            OnBuildUtil.RegisterType<EntitySystem>(OnBuild);
        }

        private static bool OnPrototypesBuild(IEnumerable<EntityPrototype> prototypes)
        {
            EntitySystemOnBuild.prototypes = prototypes.ToList();
            return true;
        }

        private static bool OnBuild(EntitySystem entitySystem)
        {
            SerializedObject so = new SerializedObject(entitySystem);

            EditorUtil.SetArrayProperty(
                so.FindProperty("entityPrototypes"),
                prototypes,
                (p, v) => p.objectReferenceValue = v);
            prototypes = null; // Cleanup.

            so.ApplyModifiedProperties();
            return true;
        }
    }
}
