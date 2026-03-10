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

        private readonly Dictionary<string, GameObject> temporaryPrefabContentsRootsByPath =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);

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
        /// <param name="scenePath"> The logical resource path associated with the temporary object. </param>
        internal void SetTemporaryAlias (
            string alias,
            UnityEngine.Object unityObject,
            string scenePath,
            string? sourceGlobalObjectId = null)
        {
            SetTemporaryAlias(
                alias,
                unityObject,
                new OperationResource(OperationTouchKind.Scene, scenePath),
                sourceGlobalObjectId);
        }

        /// <summary> Stores or replaces one temporary alias value used during plan execution. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="unityObject"> The temporary live object. </param>
        /// <param name="resource"> The logical owner resource associated with the temporary object. </param>
        /// <param name="sourceGlobalObjectId"> The optional source GlobalObjectId used to synchronize shadows. </param>
        internal void SetTemporaryAlias (
            string alias,
            UnityEngine.Object unityObject,
            OperationResource resource,
            string? sourceGlobalObjectId = null)
        {
            ValidateAlias(alias);
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            ValidateResource(resource, nameof(resource));
            if (sourceGlobalObjectId != null
                && string.IsNullOrWhiteSpace(sourceGlobalObjectId))
            {
                throw new ArgumentException("Source GlobalObjectId must not be empty when provided.", nameof(sourceGlobalObjectId));
            }

            temporaryAliasesByName[alias] = new TemporaryAliasValue(unityObject, resource, sourceGlobalObjectId);
        }

        /// <summary> Tries to get one temporary alias state. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="state"> The tracked alias state when found. </param>
        /// <returns> <see langword="true" /> when temporary alias exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetTemporaryAliasState (
            string alias,
            out TemporaryAliasState state)
        {
            state = default;
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

            state = new TemporaryAliasState(value.UnityObject, value.Resource, value.SourceGlobalObjectId);
            return true;
        }

        /// <summary> Stores or replaces one plan-time ensured component keyed by target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The source GameObject GlobalObjectId. </param>
        /// <param name="componentType"> The ensured component runtime type. </param>
        /// <param name="component"> The temporary ensured component. </param>
        /// <param name="scenePath"> The owning resource path. </param>
        internal void SetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            Component component,
            string scenePath)
        {
            SetEnsuredComponent(
                targetGlobalObjectId,
                componentType,
                component,
                new OperationResource(OperationTouchKind.Scene, scenePath));
        }

        /// <summary> Stores or replaces one plan-time ensured component keyed by target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The source GameObject GlobalObjectId. </param>
        /// <param name="componentType"> The ensured component runtime type. </param>
        /// <param name="component"> The temporary ensured component. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void SetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            Component component,
            OperationResource resource)
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

            ValidateResource(resource, nameof(resource));
            ensuredComponentsByKey[new EnsuredComponentKey(targetGlobalObjectId, componentType)] =
                new EnsuredComponentValue(component, resource);
        }

        /// <summary> Tries to get one plan-time ensured component state keyed by target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The source GameObject GlobalObjectId. </param>
        /// <param name="componentType"> The ensured component runtime type. </param>
        /// <param name="state"> The ensured component state when found. </param>
        /// <returns> <see langword="true" /> when ensured component exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetEnsuredComponentState (
            string targetGlobalObjectId,
            Type componentType,
            out EnsuredComponentState state)
        {
            state = default;
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

            state = new EnsuredComponentState(value.Component, value.Resource);
            return true;
        }

        /// <summary> Stores or replaces one temporary component shadow keyed by source GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="component"> The temporary shadow component. </param>
        /// <param name="scenePath"> The owning resource path. </param>
        internal void SetComponentShadow (
            string globalObjectId,
            Component component,
            string scenePath)
        {
            SetComponentShadow(
                globalObjectId,
                component,
                new OperationResource(OperationTouchKind.Scene, scenePath));
        }

        /// <summary> Stores or replaces one temporary component shadow keyed by source GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="component"> The temporary shadow component. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void SetComponentShadow (
            string globalObjectId,
            Component component,
            OperationResource resource)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must not be null, empty, or whitespace.", nameof(globalObjectId));
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            ValidateResource(resource, nameof(resource));
            componentShadowsByGlobalObjectId[globalObjectId] = new ComponentShadowValue(component, resource);
            SynchronizeTemporaryAliases(globalObjectId, component, resource);
        }

        /// <summary> Replaces tracked temporary component references that still point to an older plan-time component instance. </summary>
        /// <param name="sourceComponent"> The previous temporary component instance. </param>
        /// <param name="replacementComponent"> The replacement temporary component instance. </param>
        /// <param name="scenePath"> The owning resource path. </param>
        internal void ReplaceTrackedTemporaryComponent (
            Component sourceComponent,
            Component replacementComponent,
            string scenePath)
        {
            ReplaceTrackedTemporaryComponent(
                sourceComponent,
                replacementComponent,
                new OperationResource(OperationTouchKind.Scene, scenePath));
        }

        /// <summary> Replaces tracked temporary component references that still point to an older plan-time component instance. </summary>
        /// <param name="sourceComponent"> The previous temporary component instance. </param>
        /// <param name="replacementComponent"> The replacement temporary component instance. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void ReplaceTrackedTemporaryComponent (
            Component sourceComponent,
            Component replacementComponent,
            OperationResource resource)
        {
            if (sourceComponent == null)
            {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            if (replacementComponent == null)
            {
                throw new ArgumentNullException(nameof(replacementComponent));
            }

            ValidateResource(resource, nameof(resource));
            // NOTE:
            // Plan-time component mutations replace the temporary component instance with a cloned sandbox.
            // Every tracked state that still points at the previous clone must advance together or later
            // plan steps can observe stale serialized data.
            SynchronizeTemporaryAliases(sourceComponent, replacementComponent, resource);
            SynchronizeEnsuredComponents(sourceComponent, replacementComponent, resource);
        }

        /// <summary> Tries to get one temporary component shadow state. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="state"> The component shadow state when found. </param>
        /// <returns> <see langword="true" /> when shadow exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetComponentShadowState (
            string globalObjectId,
            out ComponentShadowState state)
        {
            state = default;
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

            state = new ComponentShadowState(value.Component, value.Resource);
            return true;
        }

        /// <summary> Tracks one temporary prefab-contents root for unload at the end of request execution. </summary>
        /// <param name="prefabPath"> The prefab asset path associated with the loaded contents. </param>
        /// <param name="prefabContentsRoot"> The loaded prefab-contents root. </param>
        internal void TrackTemporaryPrefabContentsRoot (
            string prefabPath,
            GameObject prefabContentsRoot)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path must not be null, empty, or whitespace.", nameof(prefabPath));
            }

            if (prefabContentsRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabContentsRoot));
            }

            temporaryPrefabContentsRootsByPath[prefabPath] = prefabContentsRoot;
        }

        /// <summary> Tries to get one request-local temporary prefab-contents root. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="prefabContentsRoot"> The loaded prefab-contents root when found. </param>
        /// <returns> <see langword="true" /> when tracked root exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetTemporaryPrefabContentsRoot (
            string prefabPath,
            out GameObject? prefabContentsRoot)
        {
            prefabContentsRoot = null;
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                return false;
            }

            if (!temporaryPrefabContentsRootsByPath.TryGetValue(prefabPath, out var value))
            {
                return false;
            }

            if (value == null)
            {
                temporaryPrefabContentsRootsByPath.Remove(prefabPath);
                return false;
            }

            prefabContentsRoot = value;
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
            foreach (var pair in temporaryPrefabContentsRootsByPath)
            {
                var prefabContentsRoot = pair.Value;
                if (prefabContentsRoot != null)
                {
                    UnityEditor.PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
                }
            }

            temporaryPrefabContentsRootsByPath.Clear();

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

        private static void ValidateResource (
            OperationResource resource,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(resource.Path))
            {
                throw new ArgumentException("Operation resource path must not be null, empty, or whitespace.", parameterName);
            }
        }

        private void SynchronizeTemporaryAliases (
            string globalObjectId,
            Component component,
            OperationResource resource)
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
                temporaryAliasesByName[alias] = new TemporaryAliasValue(component, resource, globalObjectId);
            }
        }

        private void SynchronizeTemporaryAliases (
            Component sourceComponent,
            Component replacementComponent,
            OperationResource resource)
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
                temporaryAliasesByName[alias] = new TemporaryAliasValue(replacementComponent, resource, sourceGlobalObjectId);
            }
        }

        private void SynchronizeEnsuredComponents (
            Component sourceComponent,
            Component replacementComponent,
            OperationResource resource)
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
                ensuredComponentsByKey[key] = new EnsuredComponentValue(replacementComponent, resource);
            }
        }

        private readonly struct TemporaryAliasValue
        {
            public TemporaryAliasValue (
                UnityEngine.Object unityObject,
                OperationResource resource,
                string? sourceGlobalObjectId)
            {
                UnityObject = unityObject;
                Resource = resource;
                SourceGlobalObjectId = sourceGlobalObjectId;
            }

            public UnityEngine.Object UnityObject { get; }

            public OperationResource Resource { get; }

            public string? SourceGlobalObjectId { get; }
        }

        internal readonly struct TemporaryAliasState
        {
            public TemporaryAliasState (
                UnityEngine.Object unityObject,
                OperationResource resource,
                string? sourceGlobalObjectId)
            {
                UnityObject = unityObject;
                Resource = resource;
                SourceGlobalObjectId = sourceGlobalObjectId;
            }

            public UnityEngine.Object? UnityObject { get; }

            public OperationResource Resource { get; }

            public string? SourceGlobalObjectId { get; }
        }

        private readonly struct ComponentShadowValue
        {
            public ComponentShadowValue (
                Component component,
                OperationResource resource)
            {
                Component = component;
                Resource = resource;
            }

            public Component Component { get; }

            public OperationResource Resource { get; }
        }

        internal readonly struct ComponentShadowState
        {
            public ComponentShadowState (
                Component component,
                OperationResource resource)
            {
                Component = component;
                Resource = resource;
            }

            public Component? Component { get; }

            public OperationResource Resource { get; }
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
                OperationResource resource)
            {
                Component = component;
                Resource = resource;
            }

            public Component Component { get; }

            public OperationResource Resource { get; }
        }

        internal readonly struct EnsuredComponentState
        {
            public EnsuredComponentState (
                Component component,
                OperationResource resource)
            {
                Component = component;
                Resource = resource;
            }

            public Component? Component { get; }

            public OperationResource Resource { get; }
        }
    }
}