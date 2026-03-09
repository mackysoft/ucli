using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Text;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one per-request execution context shared by operation phases. </summary>
    public sealed class OperationExecutionContext
    {
        private readonly Dictionary<string, TemporaryAliasValue> temporaryAliasesByName =
            new Dictionary<string, TemporaryAliasValue>(StringComparer.Ordinal);

        private readonly Dictionary<string, ComponentShadowValue> componentShadowsByGlobalObjectId =
            new Dictionary<string, ComponentShadowValue>(StringComparer.Ordinal);

        private readonly Dictionary<EnsuredComponentKey, EnsuredComponentValue> ensuredComponentsByKey =
            new Dictionary<EnsuredComponentKey, EnsuredComponentValue>();

        private readonly List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();

        /// <summary> Initializes a new instance of the <see cref="OperationExecutionContext" /> class. </summary>
        public OperationExecutionContext ()
            : this(new OperationAliasStore())
        {
        }

        /// <summary> Initializes a new instance of the <see cref="OperationExecutionContext" /> class. </summary>
        /// <param name="aliasStore"> The alias-store dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="aliasStore" /> is <see langword="null" />. </exception>
        internal OperationExecutionContext (OperationAliasStore aliasStore)
        {
            AliasStore = aliasStore ?? throw new ArgumentNullException(nameof(aliasStore));
        }

        /// <summary> Gets the alias store used to share resolved references within one request. </summary>
        internal OperationAliasStore AliasStore { get; }

        /// <summary> Stores or replaces one temporary alias value used during plan execution. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="unityObject"> The temporary live object. </param>
        /// <param name="scenePath"> The logical scene path associated with the temporary object. </param>
        internal void SetTemporaryAlias (
            string alias,
            UnityEngine.Object unityObject,
            string scenePath,
            string? sourceGlobalObjectId = null)
        {
            ValidateAlias(alias);
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Scene path must not be null, empty, or whitespace.", nameof(scenePath));
            }

            if (sourceGlobalObjectId != null
                && string.IsNullOrWhiteSpace(sourceGlobalObjectId))
            {
                throw new ArgumentException("Source GlobalObjectId must not be empty when provided.", nameof(sourceGlobalObjectId));
            }

            temporaryAliasesByName[alias] = new TemporaryAliasValue(unityObject, scenePath, sourceGlobalObjectId);
        }

        /// <summary> Tries to get one temporary alias value. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="unityObject"> The temporary live object when found. </param>
        /// <param name="scenePath"> The logical scene path when found. </param>
        /// <returns> <see langword="true" /> when temporary alias exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetTemporaryAlias (
            string alias,
            out UnityEngine.Object? unityObject,
            out string scenePath)
        {
            return TryGetTemporaryAlias(alias, out unityObject, out scenePath, out _);
        }

        /// <summary> Tries to get one temporary alias value together with the tracked source GlobalObjectId. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="unityObject"> The temporary live object when found. </param>
        /// <param name="scenePath"> The logical scene path when found. </param>
        /// <param name="sourceGlobalObjectId"> The source GlobalObjectId when tracked; otherwise <see langword="null" />. </param>
        /// <returns> <see langword="true" /> when temporary alias exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetTemporaryAlias (
            string alias,
            out UnityEngine.Object? unityObject,
            out string scenePath,
            out string? sourceGlobalObjectId)
        {
            unityObject = null;
            scenePath = string.Empty;
            sourceGlobalObjectId = null;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            if (!temporaryAliasesByName.TryGetValue(alias, out var value))
            {
                return false;
            }

            if (value.UnityObject == null)
            {
                temporaryAliasesByName.Remove(alias);
                return false;
            }

            unityObject = value.UnityObject;
            scenePath = value.ScenePath;
            sourceGlobalObjectId = value.SourceGlobalObjectId;
            return true;
        }

        /// <summary> Stores or replaces one plan-time ensured component keyed by target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The source GameObject GlobalObjectId. </param>
        /// <param name="componentType"> The ensured component runtime type. </param>
        /// <param name="component"> The temporary ensured component. </param>
        /// <param name="scenePath"> The owning scene path. </param>
        internal void SetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            Component component,
            string scenePath)
        {
            if (string.IsNullOrWhiteSpace(targetGlobalObjectId))
            {
                throw new ArgumentException("Target GlobalObjectId must not be null, empty, or whitespace.", nameof(targetGlobalObjectId));
            }

            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Scene path must not be null, empty, or whitespace.", nameof(scenePath));
            }

            ensuredComponentsByKey[new EnsuredComponentKey(targetGlobalObjectId, componentType)] =
                new EnsuredComponentValue(component, scenePath);
        }

        /// <summary> Tries to get one plan-time ensured component keyed by target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The source GameObject GlobalObjectId. </param>
        /// <param name="componentType"> The ensured component runtime type. </param>
        /// <param name="component"> The temporary ensured component when found. </param>
        /// <param name="scenePath"> The owning scene path when found. </param>
        /// <returns> <see langword="true" /> when ensured component exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            out Component? component,
            out string scenePath)
        {
            component = null;
            scenePath = string.Empty;
            if (string.IsNullOrWhiteSpace(targetGlobalObjectId) || componentType == null)
            {
                return false;
            }

            if (!ensuredComponentsByKey.TryGetValue(new EnsuredComponentKey(targetGlobalObjectId, componentType), out var value))
            {
                return false;
            }

            if (value.Component == null)
            {
                ensuredComponentsByKey.Remove(new EnsuredComponentKey(targetGlobalObjectId, componentType));
                return false;
            }

            component = value.Component;
            scenePath = value.ScenePath;
            return true;
        }

        /// <summary> Stores or replaces one temporary component shadow keyed by source GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="component"> The temporary shadow component. </param>
        /// <param name="scenePath"> The owning scene path. </param>
        internal void SetComponentShadow (
            string globalObjectId,
            Component component,
            string scenePath)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must not be null, empty, or whitespace.", nameof(globalObjectId));
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Scene path must not be null, empty, or whitespace.", nameof(scenePath));
            }

            componentShadowsByGlobalObjectId[globalObjectId] = new ComponentShadowValue(component, scenePath);
            SynchronizeTemporaryAliases(globalObjectId, component, scenePath);
        }

        /// <summary> Replaces tracked temporary component references that still point to an older plan-time component instance. </summary>
        /// <param name="sourceComponent"> The previous temporary component instance. </param>
        /// <param name="replacementComponent"> The replacement temporary component instance. </param>
        /// <param name="scenePath"> The owning scene path. </param>
        internal void ReplaceTrackedTemporaryComponent (
            Component sourceComponent,
            Component replacementComponent,
            string scenePath)
        {
            if (sourceComponent == null)
            {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            if (replacementComponent == null)
            {
                throw new ArgumentNullException(nameof(replacementComponent));
            }

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Scene path must not be null, empty, or whitespace.", nameof(scenePath));
            }

            // NOTE:
            // Plan-time component mutations replace the temporary component instance with a cloned sandbox.
            // Every tracked state that still points at the previous clone must advance together or later
            // plan steps can observe stale serialized data.
            SynchronizeTemporaryAliases(sourceComponent, replacementComponent, scenePath);
            SynchronizeEnsuredComponents(sourceComponent, replacementComponent, scenePath);
        }

        /// <summary> Tries to get one temporary component shadow. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="component"> The temporary shadow component when found. </param>
        /// <param name="scenePath"> The owning scene path when found. </param>
        /// <returns> <see langword="true" /> when shadow exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetComponentShadow (
            string globalObjectId,
            out Component? component,
            out string scenePath)
        {
            component = null;
            scenePath = string.Empty;
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                return false;
            }

            if (!componentShadowsByGlobalObjectId.TryGetValue(globalObjectId, out var value))
            {
                return false;
            }

            if (value.Component == null)
            {
                componentShadowsByGlobalObjectId.Remove(globalObjectId);
                return false;
            }

            component = value.Component;
            scenePath = value.ScenePath;
            return true;
        }

        /// <summary> Tracks one temporary object for cleanup at the end of request execution. </summary>
        /// <param name="unityObject"> The temporary object to destroy. </param>
        internal void TrackTemporaryObject (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            temporaryObjects.Add(unityObject);
        }

        /// <summary> Destroys all tracked temporary objects and clears temporary state. </summary>
        internal void CleanupTemporaryObjects ()
        {
            for (var i = temporaryObjects.Count - 1; i >= 0; i--)
            {
                var temporaryObject = temporaryObjects[i];
                if (temporaryObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporaryObject);
                }
            }

            temporaryObjects.Clear();
            temporaryAliasesByName.Clear();
            componentShadowsByGlobalObjectId.Clear();
            ensuredComponentsByKey.Clear();
        }

        private static void ValidateAlias (string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("Alias must not be null, empty, or whitespace.", nameof(alias));
            }

            if (StringValueValidator.HasOuterWhitespace(alias))
            {
                throw new ArgumentException("Alias must not contain leading or trailing whitespace.", nameof(alias));
            }
        }

        private void SynchronizeTemporaryAliases (
            string globalObjectId,
            Component component,
            string scenePath)
        {
            List<string>? aliasesToSynchronize = null;
            foreach (var pair in temporaryAliasesByName)
            {
                if (!string.Equals(pair.Value.SourceGlobalObjectId, globalObjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                aliasesToSynchronize ??= new List<string>();
                aliasesToSynchronize.Add(pair.Key);
            }

            if (aliasesToSynchronize == null)
            {
                return;
            }

            for (var i = 0; i < aliasesToSynchronize.Count; i++)
            {
                var alias = aliasesToSynchronize[i];
                temporaryAliasesByName[alias] = new TemporaryAliasValue(component, scenePath, globalObjectId);
            }
        }

        private void SynchronizeTemporaryAliases (
            Component sourceComponent,
            Component replacementComponent,
            string scenePath)
        {
            List<string>? aliasesToSynchronize = null;
            foreach (var pair in temporaryAliasesByName)
            {
                if (pair.Value.UnityObject != sourceComponent)
                {
                    continue;
                }

                aliasesToSynchronize ??= new List<string>();
                aliasesToSynchronize.Add(pair.Key);
            }

            if (aliasesToSynchronize == null)
            {
                return;
            }

            for (var i = 0; i < aliasesToSynchronize.Count; i++)
            {
                var alias = aliasesToSynchronize[i];
                var sourceGlobalObjectId = temporaryAliasesByName[alias].SourceGlobalObjectId;
                temporaryAliasesByName[alias] = new TemporaryAliasValue(replacementComponent, scenePath, sourceGlobalObjectId);
            }
        }

        private void SynchronizeEnsuredComponents (
            Component sourceComponent,
            Component replacementComponent,
            string scenePath)
        {
            List<EnsuredComponentKey>? keysToSynchronize = null;
            foreach (var pair in ensuredComponentsByKey)
            {
                if (pair.Value.Component != sourceComponent)
                {
                    continue;
                }

                keysToSynchronize ??= new List<EnsuredComponentKey>();
                keysToSynchronize.Add(pair.Key);
            }

            if (keysToSynchronize == null)
            {
                return;
            }

            for (var i = 0; i < keysToSynchronize.Count; i++)
            {
                var key = keysToSynchronize[i];
                ensuredComponentsByKey[key] = new EnsuredComponentValue(replacementComponent, scenePath);
            }
        }

        private readonly struct TemporaryAliasValue
        {
            public TemporaryAliasValue (
                UnityEngine.Object unityObject,
                string scenePath,
                string? sourceGlobalObjectId)
            {
                UnityObject = unityObject;
                ScenePath = scenePath;
                SourceGlobalObjectId = sourceGlobalObjectId;
            }

            public UnityEngine.Object UnityObject { get; }

            public string ScenePath { get; }

            public string? SourceGlobalObjectId { get; }
        }

        private readonly struct ComponentShadowValue
        {
            public ComponentShadowValue (
                Component component,
                string scenePath)
            {
                Component = component;
                ScenePath = scenePath;
            }

            public Component Component { get; }

            public string ScenePath { get; }
        }

        private readonly struct EnsuredComponentKey : IEquatable<EnsuredComponentKey>
        {
            public EnsuredComponentKey (
                string targetGlobalObjectId,
                Type componentType)
            {
                TargetGlobalObjectId = targetGlobalObjectId;
                ComponentType = componentType;
            }

            public string TargetGlobalObjectId { get; }

            public Type ComponentType { get; }

            public bool Equals (EnsuredComponentKey other)
            {
                return string.Equals(TargetGlobalObjectId, other.TargetGlobalObjectId, StringComparison.Ordinal)
                    && ComponentType == other.ComponentType;
            }

            public override bool Equals (object? obj)
            {
                return obj is EnsuredComponentKey other && Equals(other);
            }

            public override int GetHashCode ()
            {
                unchecked
                {
                    return ((TargetGlobalObjectId != null ? StringComparer.Ordinal.GetHashCode(TargetGlobalObjectId) : 0) * 397)
                        ^ (ComponentType != null ? ComponentType.GetHashCode() : 0);
                }
            }
        }

        private readonly struct EnsuredComponentValue
        {
            public EnsuredComponentValue (
                Component component,
                string scenePath)
            {
                Component = component;
                ScenePath = scenePath;
            }

            public Component Component { get; }

            public string ScenePath { get; }
        }
    }
}
