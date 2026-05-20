using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks component-specific plan-time shadows and ensured instances. </summary>
    internal sealed class ComponentSandboxRegistry
    {
        private readonly Dictionary<string, ComponentShadowValue> componentShadowsByGlobalObjectId =
            new Dictionary<string, ComponentShadowValue>(StringComparer.Ordinal);

        private readonly Dictionary<EnsuredComponentKey, EnsuredComponentValue> ensuredComponentsByKey =
            new Dictionary<EnsuredComponentKey, EnsuredComponentValue>();

        /// <summary> Stores the request-local component created to satisfy an ensure operation for one target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The non-empty tracking key of the GameObject that semantically owns the ensured component. </param>
        /// <param name="componentType"> The non-null component runtime type that was ensured. </param>
        /// <param name="component"> The non-null request-local component instance. </param>
        /// <param name="targetGameObject"> The non-null request-local GameObject that semantically owns <paramref name="component" />. </param>
        /// <param name="resource"> The owner resource whose path must be non-empty. </param>
        /// <exception cref="ArgumentException"> <paramref name="targetGlobalObjectId" /> or <paramref name="resource" /> path is null, empty, or whitespace. </exception>
        /// <exception cref="ArgumentNullException"> <paramref name="componentType" />, <paramref name="component" />, or <paramref name="targetGameObject" /> is <see langword="null" />. </exception>
        public void SetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            Component component,
            GameObject targetGameObject,
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

            if (targetGameObject == null)
            {
                throw new ArgumentNullException(nameof(targetGameObject));
            }

            ValidateResource(resource, nameof(resource));
            ensuredComponentsByKey[new EnsuredComponentKey(targetGlobalObjectId, componentType)] =
                new EnsuredComponentValue(component, targetGameObject, resource);
        }

        /// <summary> Tries to retrieve the live request-local component previously ensured for one target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The target GameObject tracking key. Null, empty, or whitespace values do not match. </param>
        /// <param name="componentType"> The component runtime type. A <see langword="null" /> value does not match. </param>
        /// <param name="state"> The ensured component state when the method returns <see langword="true" />; otherwise the default value. </param>
        /// <returns> <see langword="true" /> when a non-destroyed ensured component exists for the key; otherwise <see langword="false" />. </returns>
        public bool TryGetEnsuredComponentState (
            string targetGlobalObjectId,
            Type componentType,
            out EnsuredComponentState state)
        {
            state = default;
            if (string.IsNullOrWhiteSpace(targetGlobalObjectId) || componentType == null)
            {
                return false;
            }

            var key = new EnsuredComponentKey(targetGlobalObjectId, componentType);
            if (!ensuredComponentsByKey.TryGetValue(key, out var value))
            {
                return false;
            }

            if (value.Component == null)
            {
                ensuredComponentsByKey.Remove(key);
                return false;
            }

            state = new EnsuredComponentState(value.Component, value.Resource);
            return true;
        }

        /// <summary> Tries to resolve the owner resource associated with one tracked request-local component. </summary>
        /// <param name="component"> The non-null component to find in component shadows or ensured-component state. </param>
        /// <param name="resource"> The owner resource when the method returns <see langword="true" />; otherwise the default value. </param>
        /// <returns> <see langword="true" /> when <paramref name="component" /> is tracked by this registry; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="component" /> is <see langword="null" />. </exception>
        public bool TryResolveTrackedComponentResource (
            Component component,
            out OperationResource resource)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            foreach (var pair in componentShadowsByGlobalObjectId)
            {
                if (pair.Value.Component == component)
                {
                    resource = pair.Value.Resource;
                    return true;
                }
            }

            foreach (var pair in ensuredComponentsByKey)
            {
                if (pair.Value.Component == component)
                {
                    resource = pair.Value.Resource;
                    return true;
                }
            }

            resource = default;
            return false;
        }

        /// <summary> Tries to resolve the Prefab override correlation key for one tracked request-local component. </summary>
        /// <param name="component"> The non-null component to find in component shadows or ensured-component state. </param>
        /// <param name="targetKey"> The Prefab override correlation key when the method returns <see langword="true" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when <paramref name="component" /> is tracked by this registry; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="component" /> is <see langword="null" />. </exception>
        public bool TryResolveTrackedComponentTargetKey (
            Component component,
            out string targetKey)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            foreach (var pair in ensuredComponentsByKey)
            {
                if (pair.Value.Component == component)
                {
                    targetKey = pair.Key.CreateTrackingKey();
                    return true;
                }
            }

            foreach (var pair in componentShadowsByGlobalObjectId)
            {
                if (pair.Value.Component == component)
                {
                    targetKey = pair.Key;
                    return true;
                }
            }

            targetKey = string.Empty;
            return false;
        }

        /// <summary> Tries to resolve the semantic owner GameObject tracking key for one tracked request-local component. </summary>
        /// <param name="component"> The non-null component to find in component shadows or ensured-component state. </param>
        /// <param name="ownerKey"> The owner GameObject tracking key when the method returns <see langword="true" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when <paramref name="component" /> is tracked by this registry; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="component" /> is <see langword="null" />. </exception>
        public bool TryResolveTrackedComponentOwnerKey (
            Component component,
            out string ownerKey)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            foreach (var pair in componentShadowsByGlobalObjectId)
            {
                if (pair.Value.Component == component)
                {
                    ownerKey = pair.Value.OwnerGameObjectTrackingKey;
                    return true;
                }
            }

            foreach (var pair in ensuredComponentsByKey)
            {
                if (pair.Value.Component == component)
                {
                    ownerKey = pair.Key.TargetGlobalObjectId;
                    return true;
                }
            }

            ownerKey = string.Empty;
            return false;
        }

        /// <summary> Tries to resolve the semantic owner GameObject for one tracked request-local component. </summary>
        /// <param name="component"> The non-null component to find in component shadows or ensured-component state. </param>
        /// <param name="ownerGameObject"> The owner GameObject when the method returns <see langword="true" />; otherwise <see langword="null" />. </param>
        /// <returns> <see langword="true" /> when <paramref name="component" /> is tracked and its owner GameObject is still alive; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="component" /> is <see langword="null" />. </exception>
        public bool TryResolveTrackedComponentOwnerGameObject (
            Component component,
            out GameObject? ownerGameObject)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            foreach (var pair in componentShadowsByGlobalObjectId)
            {
                if (pair.Value.Component == component)
                {
                    ownerGameObject = pair.Value.OwnerGameObject;
                    return ownerGameObject != null;
                }
            }

            foreach (var pair in ensuredComponentsByKey)
            {
                if (pair.Value.Component == component)
                {
                    ownerGameObject = pair.Value.TargetGameObject;
                    return ownerGameObject != null;
                }
            }

            ownerGameObject = null;
            return false;
        }

        /// <summary> Appends non-destroyed ensured component states for one target GameObject tracking key. </summary>
        /// <param name="targetGlobalObjectId"> The non-empty target GameObject tracking key to match. </param>
        /// <param name="destination"> The non-null destination collection that receives matching component states in registry iteration order. </param>
        /// <exception cref="ArgumentException"> <paramref name="targetGlobalObjectId" /> is null, empty, or whitespace. </exception>
        /// <exception cref="ArgumentNullException"> <paramref name="destination" /> is <see langword="null" />. </exception>
        public void CollectEnsuredComponentStates (
            string targetGlobalObjectId,
            ICollection<EnsuredComponentState> destination)
        {
            if (string.IsNullOrWhiteSpace(targetGlobalObjectId))
            {
                throw new ArgumentException("Target GlobalObjectId must not be null, empty, or whitespace.", nameof(targetGlobalObjectId));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            foreach (var pair in ensuredComponentsByKey)
            {
                if (!string.Equals(pair.Key.TargetGlobalObjectId, targetGlobalObjectId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (pair.Value.Component == null)
                {
                    continue;
                }

                destination.Add(new EnsuredComponentState(pair.Value.Component, pair.Value.Resource));
            }
        }

        /// <summary> Stores or replaces a request-local component shadow keyed by its source component tracking key. </summary>
        /// <param name="globalObjectId"> The non-empty source component tracking key. </param>
        /// <param name="component"> The non-null request-local component shadow. </param>
        /// <param name="sourceComponent"> The non-null component whose serialized state was cloned into <paramref name="component" />. </param>
        /// <param name="ownerGameObject"> The non-null GameObject that owns the source component semantically. </param>
        /// <param name="ownerGameObjectTrackingKey"> The non-empty tracking key of <paramref name="ownerGameObject" />. </param>
        /// <param name="resource"> The owner resource whose path must be non-empty. </param>
        /// <param name="temporaryAliasRegistry"> The non-null alias registry synchronized with the shadow replacement. </param>
        /// <exception cref="ArgumentException"> <paramref name="globalObjectId" />, <paramref name="ownerGameObjectTrackingKey" />, or <paramref name="resource" /> path is null, empty, or whitespace. </exception>
        /// <exception cref="ArgumentNullException"> <paramref name="component" />, <paramref name="sourceComponent" />, <paramref name="ownerGameObject" />, or <paramref name="temporaryAliasRegistry" /> is <see langword="null" />. </exception>
        public void SetComponentShadow (
            string globalObjectId,
            Component component,
            Component sourceComponent,
            GameObject ownerGameObject,
            string ownerGameObjectTrackingKey,
            OperationResource resource,
            TemporaryAliasRegistry temporaryAliasRegistry)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must not be null, empty, or whitespace.", nameof(globalObjectId));
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (sourceComponent == null)
            {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            if (ownerGameObject == null)
            {
                throw new ArgumentNullException(nameof(ownerGameObject));
            }

            if (string.IsNullOrWhiteSpace(ownerGameObjectTrackingKey))
            {
                throw new ArgumentException("Owner GameObject tracking key must not be null, empty, or whitespace.", nameof(ownerGameObjectTrackingKey));
            }

            if (temporaryAliasRegistry == null)
            {
                throw new ArgumentNullException(nameof(temporaryAliasRegistry));
            }

            ValidateResource(resource, nameof(resource));
            componentShadowsByGlobalObjectId[globalObjectId] = new ComponentShadowValue(
                component,
                sourceComponent,
                ownerGameObject,
                ownerGameObjectTrackingKey,
                resource);
            temporaryAliasRegistry.SynchronizeBySourceGlobalObjectId(globalObjectId, component, resource);
        }

        /// <summary> Replaces a tracked temporary component instance while preserving all registry keys that pointed at the source instance. </summary>
        /// <param name="sourceComponent"> The non-null tracked component instance to replace. </param>
        /// <param name="replacementComponent"> The non-null replacement component instance. </param>
        /// <param name="resource"> The owner resource whose path must be non-empty. </param>
        /// <param name="temporaryAliasRegistry"> The non-null alias registry synchronized with the replacement. </param>
        /// <exception cref="ArgumentException"> <paramref name="resource" /> path is null, empty, or whitespace. </exception>
        /// <exception cref="ArgumentNullException"> <paramref name="sourceComponent" />, <paramref name="replacementComponent" />, or <paramref name="temporaryAliasRegistry" /> is <see langword="null" />. </exception>
        public void ReplaceTrackedTemporaryComponent (
            Component sourceComponent,
            Component replacementComponent,
            OperationResource resource,
            TemporaryAliasRegistry temporaryAliasRegistry)
        {
            if (sourceComponent == null)
            {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            if (replacementComponent == null)
            {
                throw new ArgumentNullException(nameof(replacementComponent));
            }

            if (temporaryAliasRegistry == null)
            {
                throw new ArgumentNullException(nameof(temporaryAliasRegistry));
            }

            ValidateResource(resource, nameof(resource));

            // NOTE:
            // Plan-time component mutations replace the temporary component instance with a cloned sandbox.
            // Every tracked state that still points at the previous clone must advance together or later
            // plan steps can observe stale serialized data.
            temporaryAliasRegistry.ReplaceTrackedObject(sourceComponent, replacementComponent, resource);
            SynchronizeEnsuredComponents(sourceComponent, replacementComponent, resource);
        }

        /// <summary> Tries to retrieve the live component shadow for one source component tracking key. </summary>
        /// <param name="globalObjectId"> The source component tracking key. Null, empty, or whitespace values do not match. </param>
        /// <param name="state"> The component shadow state when the method returns <see langword="true" />; otherwise the default value. </param>
        /// <returns> <see langword="true" /> when a non-destroyed component shadow exists for the key; otherwise <see langword="false" />. </returns>
        public bool TryGetComponentShadowState (
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

            state = new ComponentShadowState(value.Component, value.SourceComponent, value.Resource);
            return true;
        }

        /// <summary> Clears all component shadows and ensured-component states. </summary>
        public void Clear ()
        {
            componentShadowsByGlobalObjectId.Clear();
            ensuredComponentsByKey.Clear();
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
                ensuredComponentsByKey[key] = new EnsuredComponentValue(
                    replacementComponent,
                    ensuredComponentsByKey[key].TargetGameObject,
                    resource);
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

        private readonly struct ComponentShadowValue
        {
            public ComponentShadowValue (
                Component component,
                Component sourceComponent,
                GameObject ownerGameObject,
                string ownerGameObjectTrackingKey,
                OperationResource resource)
            {
                Component = component;
                SourceComponent = sourceComponent;
                OwnerGameObject = ownerGameObject;
                OwnerGameObjectTrackingKey = ownerGameObjectTrackingKey;
                Resource = resource;
            }

            public Component Component { get; }

            public Component SourceComponent { get; }

            public GameObject OwnerGameObject { get; }

            public string OwnerGameObjectTrackingKey { get; }

            public OperationResource Resource { get; }
        }

        internal readonly struct ComponentShadowState
        {
            /// <summary> Initializes a new instance of the <see cref="ComponentShadowState" /> struct. </summary>
            /// <param name="component"> The live request-local component shadow. </param>
            /// <param name="sourceComponent"> The source component whose serialized state was cloned into <paramref name="component" />. </param>
            /// <param name="resource"> The resource that owns the component shadow. </param>
            public ComponentShadowState (
                Component component,
                Component sourceComponent,
                OperationResource resource)
            {
                Component = component;
                SourceComponent = sourceComponent;
                Resource = resource;
            }

            /// <summary> Gets the live request-local component shadow, or <see langword="null" /> when the source object has been destroyed. </summary>
            public Component? Component { get; }

            /// <summary> Gets the source component whose serialized state was cloned into <see cref="Component" />, or <see langword="null" /> when it has been destroyed. </summary>
            public Component? SourceComponent { get; }

            /// <summary> Gets the resource that owns the component shadow. </summary>
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

            public string CreateTrackingKey ()
            {
                return $"ensured:{TargetGlobalObjectId}:{ComponentType.AssemblyQualifiedName}";
            }
        }

        private readonly struct EnsuredComponentValue
        {
            public EnsuredComponentValue (
                Component component,
                GameObject targetGameObject,
                OperationResource resource)
            {
                Component = component;
                TargetGameObject = targetGameObject;
                Resource = resource;
            }

            public Component Component { get; }

            public GameObject TargetGameObject { get; }

            public OperationResource Resource { get; }
        }

        internal readonly struct EnsuredComponentState
        {
            /// <summary> Initializes a new instance of the <see cref="EnsuredComponentState" /> struct. </summary>
            /// <param name="component"> The live request-local component created by an ensure operation. </param>
            /// <param name="resource"> The resource that owns the ensured component. </param>
            public EnsuredComponentState (
                Component component,
                OperationResource resource)
            {
                Component = component;
                Resource = resource;
            }

            /// <summary> Gets the live request-local component created by an ensure operation, or <see langword="null" /> when it has been destroyed. </summary>
            public Component? Component { get; }

            /// <summary> Gets the resource that owns the ensured component. </summary>
            public OperationResource Resource { get; }
        }
    }
}
