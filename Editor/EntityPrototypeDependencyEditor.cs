using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    public static class EntityPrototypeDependencyOnBuild
    {
        private static List<EntityPrototype> prototypes;

        [OrderedInitializeOnLoad]
        private static void OnAssemblyLoad()
        {
            OnBuildUtil.RegisterTypeCumulative<EntityPrototype>(OnPrototypesBuild, order: -101);
            OnBuildUtil.RegisterTypeCumulative<EntityPrototypeDependency>(OnDependenciesBuild, order: -100);
        }

        private static bool OnPrototypesBuild(IEnumerable<EntityPrototype> prototypes)
        {
            EntityPrototypeDependencyOnBuild.prototypes = prototypes.ToList();
            return true;
        }

        private static bool OnDependenciesBuild(IEnumerable<EntityPrototypeDependency> dependencies)
        {
            if (!dependencies.Any())
                return true;
            HashSet<string> prototypeGuidsInScene = prototypes
                .Where(p => !string.IsNullOrEmpty(p.PrototypeDefinitionGuid))
                .Select(p => p.PrototypeDefinitionGuid)
                .ToHashSet();
            bool searchedForAParent = false;
            Transform parent = null;
            foreach (EntityPrototypeDefinition definition in dependencies
                .Where(d => d.prototypes != null)
                .SelectMany(d => d.prototypes))
            {
                string definitionGuid = EditorUtil.GetAssetGuidOrEmpty(definition);
                if (prototypeGuidsInScene.Contains(definitionGuid))
                    continue;
                if (!searchedForAParent)
                {
                    searchedForAParent = true;
                    parent = EntityPrototypeDefinitionEditor.FindEntityPrototypesParent(prototypes);
                }
                EntityPrototypeDefinitionEditor.AddEntityPrototypeToScene(definition, parent, "Add Entity Prototype Due To Dependency");
                prototypeGuidsInScene.Add(definitionGuid);
                OnBuildUtil.MarkForRerunDueToScriptInstantiation();
            }
            return true;
        }
    }
}
