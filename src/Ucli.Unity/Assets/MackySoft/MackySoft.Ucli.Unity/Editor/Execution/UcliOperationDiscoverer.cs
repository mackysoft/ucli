using System;
using System.Collections.Generic;
using System.Reflection;
using MackySoft.Ucli.Unity.Execution.Phases;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

using RuntimeAssembly = System.Reflection.Assembly;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution
{
    /// <summary> Discovers and instantiates operation types marked with <see cref="UcliOperationAttribute" />. </summary>
    internal static class UcliOperationDiscoverer
    {
        private const string NUnitFrameworkAssemblyName = "nunit.framework";

        /// <summary> Discovers operation instances from currently loaded assemblies. </summary>
        /// <param name="serviceProvider"> The service provider used to activate operation instances. </param>
        /// <returns> The discovered operation registration list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceProvider" /> is <see langword="null" />. </exception>
        internal static IReadOnlyList<UcliOperationRegistration> Discover (IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var types = ResolveDiscoveryTypes(
                includeUcliDefinedAssemblies: true,
                includeUserDefinedAssemblies: true,
                allowedAssemblyNames: null);

            return DiscoverFromTypes(types, serviceProvider);
        }

        /// <summary> Discovers operation instances from currently loaded assemblies with source-kind filtering. </summary>
        /// <param name="includeUcliDefinedAssemblies"> Whether built-in uCLI operation assemblies should be discovered. </param>
        /// <param name="includeUserDefinedAssemblies"> Whether user-defined operation assemblies should be discovered. </param>
        /// <param name="serviceProvider"> The service provider used to activate operation instances. </param>
        /// <returns> The discovered operation registration list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceProvider" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when one discovered operation type is invalid. </exception>
        internal static IReadOnlyList<UcliOperationRegistration> Discover (
            bool includeUcliDefinedAssemblies,
            bool includeUserDefinedAssemblies,
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var types = ResolveDiscoveryTypes(
                includeUcliDefinedAssemblies,
                includeUserDefinedAssemblies,
                allowedAssemblyNames: null);

            return DiscoverFromTypes(types, serviceProvider);
        }

        /// <summary> Discovers operation instances from a specified assembly set. </summary>
        /// <param name="assemblies"> The assembly set to inspect. </param>
        /// <param name="includeUcliDefinedAssemblies"> Whether built-in uCLI operation assemblies should be discovered. </param>
        /// <param name="includeUserDefinedAssemblies"> Whether user-defined operation assemblies should be discovered. </param>
        /// <param name="serviceProvider"> The service provider used to activate operation instances. </param>
        /// <returns> The discovered operation registration list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assemblies" /> or <paramref name="serviceProvider" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when one discovered operation type is invalid. </exception>
        internal static IReadOnlyList<UcliOperationRegistration> Discover (
            IReadOnlyList<RuntimeAssembly> assemblies,
            bool includeUcliDefinedAssemblies,
            bool includeUserDefinedAssemblies,
            IServiceProvider serviceProvider)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var allowedAssemblyNames = CreateAssemblyNameSet(assemblies);
            var types = ResolveDiscoveryTypes(
                includeUcliDefinedAssemblies,
                includeUserDefinedAssemblies,
                allowedAssemblyNames);

            return DiscoverFromTypes(types, serviceProvider);
        }

        /// <summary> Discovers operation instances from a specified type set. </summary>
        /// <param name="types"> The candidate type set. </param>
        /// <param name="serviceProvider"> The service provider used to activate operation instances. </param>
        /// <returns> The discovered operation registration list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="types" /> or <paramref name="serviceProvider" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when one discovered operation type is invalid. </exception>
        internal static IReadOnlyList<UcliOperationRegistration> DiscoverFromTypes (
            IReadOnlyList<Type?> types,
            IServiceProvider serviceProvider)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
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

                var instance = CreateOperationInstance(type, serviceProvider);
                var metadata = instance.Metadata;
                if (metadata == null)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' returned null metadata.");
                }

                ValidateTypedOperationContract(type, metadata);

                registrations.Add(new UcliOperationRegistration(metadata, instance));
            }

            return registrations;
        }

        /// <summary> Resolves operation candidate types from Unity's editor type cache. </summary>
        /// <param name="includeUcliDefinedAssemblies"> Whether built-in uCLI assemblies should be inspected. </param>
        /// <param name="includeUserDefinedAssemblies"> Whether user-defined assemblies should be inspected. </param>
        /// <param name="allowedAssemblyNames"> The optional allowed assembly-name set. </param>
        /// <returns> The discoverable operation type candidates. </returns>
        private static IReadOnlyList<Type?> ResolveDiscoveryTypes (
            bool includeUcliDefinedAssemblies,
            bool includeUserDefinedAssemblies,
            HashSet<string>? allowedAssemblyNames)
        {
            var ucliAssemblyName = typeof(UcliOperationDiscoverer).Assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(ucliAssemblyName))
            {
                throw new InvalidOperationException("The current uCLI operation assembly name is not available.");
            }

            var types = new List<Type?>();
            foreach (var type in TypeCache.GetTypesWithAttribute<UcliOperationAttribute>())
            {
                if (type == null)
                {
                    continue;
                }

                var assembly = type.Assembly;
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrWhiteSpace(assemblyName)
                    || IsTestAssembly(assembly)
                    || (allowedAssemblyNames != null && !allowedAssemblyNames.Contains(assemblyName)))
                {
                    continue;
                }

                var isUcliDefinedAssembly = StringComparer.Ordinal.Equals(assemblyName, ucliAssemblyName);
                if ((isUcliDefinedAssembly && !includeUcliDefinedAssemblies)
                    || (!isUcliDefinedAssembly && !includeUserDefinedAssemblies))
                {
                    continue;
                }

                types.Add(type);
            }

            types.Sort(CompareTypesByAssemblyAndFullName);
            return types;
        }

        private static HashSet<string> CreateAssemblyNameSet (IReadOnlyList<RuntimeAssembly> assemblies)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Count; assemblyIndex++)
            {
                var assembly = assemblies[assemblyIndex];
                if (assembly == null)
                {
                    continue;
                }

                var assemblyName = assembly.GetName().Name;
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    names.Add(assemblyName);
                }
            }

            return names;
        }

        private static bool IsTestAssembly (RuntimeAssembly assembly)
        {
            return ReferencesAssembly(assembly.GetReferencedAssemblies(), NUnitFrameworkAssemblyName);
        }

        private static bool ReferencesAssembly (
            IReadOnlyList<AssemblyName> references,
            string targetAssemblyName)
        {
            for (var referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
            {
                var reference = references[referenceIndex];
                if (StringComparer.Ordinal.Equals(reference.Name, targetAssemblyName))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareTypesByAssemblyAndFullName (
            Type? x,
            Type? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            var assemblyComparison = StringComparer.Ordinal.Compare(
                x.Assembly.GetName().Name ?? string.Empty,
                y.Assembly.GetName().Name ?? string.Empty);
            if (assemblyComparison != 0)
            {
                return assemblyComparison;
            }

            return StringComparer.Ordinal.Compare(x.FullName ?? x.Name, y.FullName ?? y.Name);
        }

        /// <summary> Determines whether one operation type is creatable through reflection. </summary>
        /// <param name="type"> The operation type candidate. </param>
        /// <returns> <see langword="true" /> when type is creatable; otherwise <see langword="false" />. </returns>
        private static bool IsCreatableOperationType (Type type)
        {
            return type.IsClass && !type.IsAbstract && !type.IsGenericTypeDefinition;
        }

        /// <summary> Validates that typed operation metadata matches the operation implementation contract. </summary>
        /// <param name="type"> The operation implementation type. </param>
        /// <param name="metadata"> The operation metadata returned by the instance. </param>
        /// <exception cref="InvalidOperationException"> Thrown when the typed operation contract and metadata disagree. </exception>
        private static void ValidateTypedOperationContract (
            Type type,
            UcliOperationMetadata metadata)
        {
            var typedOperationInterface = FindTypedOperationInterface(type);
            if (typedOperationInterface == null)
            {
                return;
            }

            var typeArguments = typedOperationInterface.GetGenericArguments();
            var argsType = typeArguments[0];
            var resultType = typeArguments[1];
            if (metadata.ArgsType != argsType
                || metadata.ResultType != resultType)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' implements '{nameof(IUcliOperation)}<{argsType.Name}, {resultType.Name}>' but metadata declares '{metadata.ArgsType.Name}' args and '{metadata.ResultType.Name}' result.");
            }
        }

        /// <summary> Finds the single typed operation interface implemented by an operation type. </summary>
        /// <param name="type"> The operation implementation type. </param>
        /// <returns> The typed operation interface, or <see langword="null" /> for untyped operations. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when multiple typed operation contracts are implemented. </exception>
        private static Type? FindTypedOperationInterface (Type type)
        {
            Type? typedOperationInterface = null;
            var interfaces = type.GetInterfaces();
            for (var interfaceIndex = 0; interfaceIndex < interfaces.Length; interfaceIndex++)
            {
                var interfaceType = interfaces[interfaceIndex];
                if (!interfaceType.IsGenericType
                    || interfaceType.GetGenericTypeDefinition() != typeof(IUcliOperation<,>))
                {
                    continue;
                }

                if (typedOperationInterface != null)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' implements multiple typed operation contracts.");
                }

                typedOperationInterface = interfaceType;
            }

            return typedOperationInterface;
        }

        /// <summary> Creates one operation instance through dependency injection. </summary>
        /// <param name="type"> The operation type. </param>
        /// <param name="serviceProvider"> The service provider used to activate the operation instance. </param>
        /// <returns> The created operation instance. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when type cannot be instantiated. </exception>
        private static IUcliOperation CreateOperationInstance (
            Type type,
            IServiceProvider serviceProvider)
        {
            try
            {
                var created = ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, type) as IUcliOperation;
                if (created == null)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' could not be instantiated as '{nameof(IUcliOperation)}'.");
                }

                return created;
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' with '{nameof(UcliOperationAttribute)}' could not be activated through dependency injection.",
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
