using System;
using System.Reflection;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals
{
    /// <summary> Resolves inherited Unity Editor members used by the screenshot integration boundary. </summary>
    internal static class UnityEditorReflection
    {
        internal const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal const BindingFlags StaticMembers =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        public static FieldInfo FindField (Type type, string name)
        {
            while (type != null)
            {
                var field = type.GetField(name, InstanceMembers);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        public static PropertyInfo FindProperty (
            Type type,
            string name,
            BindingFlags bindingFlags = InstanceMembers)
        {
            while (type != null)
            {
                var property = type.GetProperty(name, bindingFlags);
                if (property != null)
                {
                    return property;
                }

                type = type.BaseType;
            }

            return null;
        }

        public static MethodInfo FindMethod (
            Type type,
            string name,
            Type[] parameterTypes)
        {
            while (type != null)
            {
                var method = type.GetMethod(
                    name,
                    InstanceMembers,
                    binder: null,
                    parameterTypes,
                    modifiers: null);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        public static Exception UnwrapInvocationException (Exception exception)
        {
            return exception is TargetInvocationException { InnerException: not null } invocationException
                ? invocationException.InnerException
                : exception;
        }
    }
}
