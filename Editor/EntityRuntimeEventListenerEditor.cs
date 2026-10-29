using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    public static class EntityRuntimeEventListenerOnBuild
    {
        [OrderedInitializeOnLoad]
        public static void OnAssemblyLoad()
        {
            if (!ValidateEntityRuntimeEventListenerAttributes())
                OnBuildUtil.RegisterAction(ValidateEntityRuntimeEventListenerAttributes, order: -1000000);
        }

        private static bool ValidateEntityRuntimeEventListenerAttributes()
        {
            bool valid = true;
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<EntityRuntimeEventListenerAttribute>())
            {
                EntityRuntimeEventListenerAttribute attr = method.GetCustomAttribute<EntityRuntimeEventListenerAttribute>(inherit: true);
                if (!method.IsPublic)
                {
                    Debug.LogError($"[EntitySystem] The method {method.DeclaringType.Name}.{method.Name} is marked "
                        + $"as an entity runtime event listener, however event listeners must be public.");
                    valid = false;
                }
                if (method.Name != attr.EventName)
                {
                    Debug.LogError($"[EntitySystem] The method {method.DeclaringType.Name}.{method.Name} is marked "
                        + $"as an entity runtime event listener, however the expected event name is {attr.EventName}.");
                    valid = false;
                }
            }
            return valid;
        }
    }
}
