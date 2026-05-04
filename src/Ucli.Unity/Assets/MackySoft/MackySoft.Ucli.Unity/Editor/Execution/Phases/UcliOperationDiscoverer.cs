using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.Compilation;

using RuntimeAssembly = System.Reflection.Assembly;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Discovers and instantiates operation types marked with <see cref="UcliOperationAttribute" />. </summary>
    internal static class UcliOperationDiscoverer
    {
        private const string NUnitFrameworkAssemblyName = "nunit.framework";

        /// <summary> Discovers operation instances from currently loaded assemblies. </summary>
        /// <returns> The discovered operation registration list. </returns>
        public static IReadOnlyList<UcliOperationRegistration> Discover ()
        {
            return Discover(
                includeUcliDefinedAssemblies: true,
                includeUserDefinedAssemblies: true);
        }

        /// <summary> Discovers operation instances from currently loaded assemblies with source-kind filtering. </summary>
        /// <param name="includeUcliDefinedAssemblies"> Whether built-in uCLI operation assemblies should be discovered. </param>
        /// <param name="includeUserDefinedAssemblies"> Whether user-defined operation assemblies should be discovered. </param>
        /// <returns> The discovered operation registration list. </returns>
        internal static IReadOnlyList<UcliOperationRegistration> Discover (
            bool includeUcliDefinedAssemblies,
            bool includeUserDefinedAssemblies)
        {
            return Discover(
                AppDomain.CurrentDomain.GetAssemblies(),
                includeUcliDefinedAssemblies,
                includeUserDefinedAssemblies);
        }

        /// <summary> Discovers operation instances from a specified assembly set. </summary>
        /// <param name="assemblies"> The assembly set to inspect. </param>
        /// <param name="includeUcliDefinedAssemblies"> Whether built-in uCLI operation assemblies should be discovered. </param>
        /// <param name="includeUserDefinedAssemblies"> Whether user-defined operation assemblies should be discovered. </param>
        /// <returns> The discovered operation registration list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assemblies" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when one discovered operation type is invalid. </exception>
        internal static IReadOnlyList<UcliOperationRegistration> Discover (
            IReadOnlyList<RuntimeAssembly> assemblies,
            bool includeUcliDefinedAssemblies,
            bool includeUserDefinedAssemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var ucliAssemblyName = typeof(UcliOperationDiscoverer).Assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(ucliAssemblyName))
            {
                throw new InvalidOperationException("The current uCLI operation assembly name is not available.");
            }

            var discoveryAssemblyNames = ResolveDiscoveryAssemblyNames(
                CompilationPipeline.GetAssemblies(AssembliesType.Editor),
                assemblies,
                ucliAssemblyName,
                includeUcliDefinedAssemblies,
                includeUserDefinedAssemblies);

            var registrations = new List<UcliOperationRegistration>();
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Count; assemblyIndex++)
            {
                var assembly = assemblies[assemblyIndex];
                if (assembly == null)
                {
                    continue;
                }

                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrWhiteSpace(assemblyName)
                    || !discoveryAssemblyNames.Contains(assemblyName))
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

                ValidateTypedOperationContract(type, metadata);

                registrations.Add(new UcliOperationRegistration(metadata, instance));
            }

            return registrations;
        }

        /// <summary> Gets all loadable types from one assembly, tolerating partial load failures. </summary>
        /// <param name="assembly"> The source assembly. </param>
        /// <returns> Loadable type list. </returns>
        private static IReadOnlyList<Type?> GetLoadableTypes (RuntimeAssembly assembly)
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

        /// <summary> Resolves loaded assembly names that can define uCLI operations. </summary>
        /// <param name="compilationAssemblies"> The Unity editor compilation assemblies. </param>
        /// <param name="loadedAssemblies"> The currently loaded runtime assemblies. </param>
        /// <param name="ucliAssemblyName"> The built-in uCLI editor assembly name. </param>
        /// <param name="includeUcliDefinedAssemblies"> Whether built-in uCLI assemblies should be inspected. </param>
        /// <param name="includeUserDefinedAssemblies"> Whether user-defined assemblies should be inspected. </param>
        /// <returns> The discoverable loaded assembly names. </returns>
        private static HashSet<string> ResolveDiscoveryAssemblyNames (
            IReadOnlyList<UnityEditor.Compilation.Assembly> compilationAssemblies,
            IReadOnlyList<RuntimeAssembly> loadedAssemblies,
            string ucliAssemblyName,
            bool includeUcliDefinedAssemblies,
            bool includeUserDefinedAssemblies)
        {
            var loadedAssembliesByName = new Dictionary<string, RuntimeAssembly>(StringComparer.Ordinal);
            for (var loadedAssemblyIndex = 0; loadedAssemblyIndex < loadedAssemblies.Count; loadedAssemblyIndex++)
            {
                var loadedAssembly = loadedAssemblies[loadedAssemblyIndex];
                if (loadedAssembly == null)
                {
                    continue;
                }

                var loadedAssemblyName = loadedAssembly.GetName().Name;
                if (string.IsNullOrWhiteSpace(loadedAssemblyName)
                    || loadedAssembliesByName.ContainsKey(loadedAssemblyName))
                {
                    continue;
                }

                loadedAssembliesByName.Add(loadedAssemblyName, loadedAssembly);
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var compilationAssemblyIndex = 0; compilationAssemblyIndex < compilationAssemblies.Count; compilationAssemblyIndex++)
            {
                var compilationAssembly = compilationAssemblies[compilationAssemblyIndex];
                if (compilationAssembly == null
                    || string.IsNullOrWhiteSpace(compilationAssembly.name)
                    || !loadedAssembliesByName.ContainsKey(compilationAssembly.name))
                {
                    continue;
                }

                if (StringComparer.Ordinal.Equals(compilationAssembly.name, ucliAssemblyName))
                {
                    if (includeUcliDefinedAssemblies)
                    {
                        names.Add(compilationAssembly.name);
                    }

                    continue;
                }

                if (!includeUserDefinedAssemblies
                    || !ReferencesAssembly(compilationAssembly.assemblyReferences, ucliAssemblyName)
                    || ReferencesCompiledAssembly(
                        compilationAssembly.compiledAssemblyReferences,
                        NUnitFrameworkAssemblyName))
                {
                    continue;
                }

                names.Add(compilationAssembly.name);
            }

            return names;
        }

        /// <summary> Determines whether the compilation assembly references one target assembly by identity. </summary>
        /// <param name="references"> The referenced compilation assembly list. </param>
        /// <param name="targetAssemblyName"> The target assembly simple name. </param>
        /// <returns> <see langword="true" /> when one reference matches the target assembly; otherwise <see langword="false" />. </returns>
        private static bool ReferencesAssembly (
            IReadOnlyList<UnityEditor.Compilation.Assembly> references,
            string targetAssemblyName)
        {
            for (var referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
            {
                var reference = references[referenceIndex];
                if (reference != null
                    && StringComparer.Ordinal.Equals(reference.name, targetAssemblyName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary> Determines whether the compilation assembly references one compiled assembly by identity. </summary>
        /// <param name="references"> The compiled reference path list. </param>
        /// <param name="targetAssemblyName"> The target assembly simple name. </param>
        /// <returns> <see langword="true" /> when one compiled reference matches the target assembly; otherwise <see langword="false" />. </returns>
        private static bool ReferencesCompiledAssembly (
            IReadOnlyList<string> references,
            string targetAssemblyName)
        {
            // NOTE: UnityEditor.Compilation in Unity 2023.2 does not expose a public
            // Editor-test flag. For user-defined uCLI candidate assemblies only, the
            // compiled reference list is the smallest public signal that distinguishes
            // test assemblies from real operation assemblies.
            for (var referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
            {
                var referencePath = references[referenceIndex];
                if (string.IsNullOrWhiteSpace(referencePath))
                {
                    continue;
                }

                var referenceAssemblyName = Path.GetFileNameWithoutExtension(referencePath);
                if (StringComparer.Ordinal.Equals(referenceAssemblyName, targetAssemblyName))
                {
                    return true;
                }
            }

            return false;
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
