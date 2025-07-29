using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class EntitySystemOnBuild
    {
        private static List<EntityPrototype> prototypes;

        static EntitySystemOnBuild()
        {
            OnBuildUtil.RegisterTypeCumulative<EntityPrototype>(OnPrototypesBuild, order: -1);
            OnBuildUtil.RegisterType<EntitySystem>(OnBuild, order: 0);
        }

        private static bool OnPrototypesBuild(IEnumerable<EntityPrototype> prototypes)
        {
            EntitySystemOnBuild.prototypes = prototypes.ToList();
            return true;
        }

        private static bool OnBuild(EntitySystem entitySystem)
        {
            SerializedObject so = new SerializedObject(entitySystem);

            List<string> rawExtensionMethodNamesLut = prototypes
                .SelectMany(p => p.DefaultEntityInst.extensions)
                .Select(e =>
                {
                    if (EntitySystemEditorUtil.IsEntityExtension(e.GetType(), out System.Type extensionDataType))
                        return extensionDataType;
                    Debug.LogError($"[EntitySystem] Impossible, an entity prototype's default entity has "
                        + $"an extension that is not in the extension types lut.", e);
                    return null;
                })
                .Where(t => t != null)
                .Distinct()
                .OrderBy(t => t.Name)
                .Select(t => (
                    className: t.Name,
                    iaHandlers: EditorUtil.GetMethodsIncludingBase(t, BindingFlags.Instance | BindingFlags.Public, typeof(UdonSharpBehaviour))
                        .Where(m => m.GetCustomAttribute<EntityExtensionDataInputActionAttribute>() != null)
                        .Select(m => m.Name)
                        .OrderBy(n => n)
                        .ToList()
                    ))
                .Aggregate(new List<string>(), (list, e) =>
                {
                    list.Add(e.className);
                    list.Add(e.iaHandlers.Count.ToString("d"));
                    list.AddRange(e.iaHandlers);
                    return list;
                });
            EditorUtil.SetArrayProperty(
                so.FindProperty("rawExtensionMethodNamesLut"),
                rawExtensionMethodNamesLut,
                (p, v) => p.stringValue = v);

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
