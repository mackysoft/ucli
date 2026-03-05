using System;
using System.Collections.Generic;
using System.Reflection;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Discovers and instantiates operation types marked with <see cref="UcliOperationAttribute" />. </summary>
    internal static class UcliOperationDiscoverer
    {
        /// <summary> Discovers operation instances from currently loaded assemblies. </summary>
        /// <returns> The discovered operation registration list. </returns>
        public static IReadOnlyList<UcliOperationRegistration> Discover ()
        {
            return Discover(AppDomain.CurrentDomain.GetAssemblies());
        }

        /// <summary> Discovers operation instances from a specified assembly set. </summary>
        /// <param name="assemblies"> The assembly set to inspect. </param>
        /// <returns> The discovered operation registration list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assemblies" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when one discovered operation type is invalid. </exception>
        internal static IReadOnlyList<UcliOperationRegistration> Discover (IReadOnlyList<Assembly> assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var registrations = new List<UcliOperationRegistration>();
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Count; assemblyIndex++)
            {
                var assembly = assemblies[assemblyIndex];
                if (assembly == null)
                {
                    continue;
                }

                var types = GetLoadableTypes(assembly);
                var discoveredFromAssembly = DiscoverFromTypes(types);
                for (var operationIndex = 0; operationIndex < discoveredFromAssembly.Count; operationIndex++)
                {
                    registrations.Add(discoveredFromAssembly[operationIndex]);
                }
            }

            return registrations;
        }

        /// <summary> Discovers operation instances from a specified type set. </summary>
        /// <param name="types"> The candidate type set. </param>
        /// <returns> The discovered operation registration list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="types" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when one discovered operation type is invalid. </exception>
        internal static IReadOnlyList<UcliOperationRegistration> DiscoverFromTypes (IReadOnlyList<Type?> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            var registrations = new List<UcliOperationRegistration>();
            for (var typeIndex = 0; typeIndex < types.Count; typeIndex++)
            {
                var type = types[typeIndex];
                if (type == null)
                {
                    continue;
                }

                var operationAttribute = type.GetCustomAttribute<UcliOperationAttribute>(inherit: false);
                if (operationAttribute == null)
                {
                    continue;
                }

                if (!typeof(IUcliOperation).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' has '{nameof(UcliOperationAttribute)}' but does not implement '{nameof(IUcliOperation)}'.");
                }

                if (!IsCreatableOperationType(type))
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' with '{nameof(UcliOperationAttribute)}' must be a non-abstract, non-generic class.");
                }

                var instance = CreateOperationInstance(type);
                var metadata = instance.Metadata;
                if (metadata == null)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' returned null metadata.");
                }

                registrations.Add(new UcliOperationRegistration(metadata, instance));
            }

            return registrations;
        }

        /// <summary> Gets all loadable types from one assembly, tolerating partial load failures. </summary>
        /// <param name="assembly"> The source assembly. </param>
        /// <returns> Loadable type list. </returns>
        private static IReadOnlyList<Type?> GetLoadableTypes (Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        /// <summary> Determines whether one operation type is creatable through reflection. </summary>
        /// <param name="type"> The operation type candidate. </param>
        /// <returns> <see langword="true" /> when type is creatable; otherwise <see langword="false" />. </returns>
        private static bool IsCreatableOperationType (Type type)
        {
            return type.IsClass && !type.IsAbstract && !type.IsGenericTypeDefinition;
        }

        /// <summary> Creates one operation instance through parameterless constructor invocation. </summary>
        /// <param name="type"> The operation type. </param>
        /// <returns> The created operation instance. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when type cannot be instantiated. </exception>
        private static IUcliOperation CreateOperationInstance (Type type)
        {
            try
            {
                var created = Activator.CreateInstance(type) as IUcliOperation;
                if (created == null)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' could not be instantiated as '{nameof(IUcliOperation)}'.");
                }

                return created;
            }
            catch (MissingMethodException exception)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' with '{nameof(UcliOperationAttribute)}' requires a public parameterless constructor.",
                    exception);
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' constructor threw an exception during operation discovery.",
                    exception);
            }
            catch (MemberAccessException exception)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' with '{nameof(UcliOperationAttribute)}' is not accessible for operation discovery.",
                    exception);
            }
        }
    }
}
