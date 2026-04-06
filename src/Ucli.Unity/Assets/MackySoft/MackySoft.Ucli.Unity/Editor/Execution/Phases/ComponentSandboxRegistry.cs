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

        public void SetEnsuredComponent (
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

        public void SetComponentShadow (
            string globalObjectId,
            Component component,
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

            if (temporaryAliasRegistry == null)
            {
                throw new ArgumentNullException(nameof(temporaryAliasRegistry));
            }

            ValidateResource(resource, nameof(resource));
            componentShadowsByGlobalObjectId[globalObjectId] = new ComponentShadowValue(component, resource);
            temporaryAliasRegistry.SynchronizeBySourceGlobalObjectId(globalObjectId, component, resource);
        }

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

            state = new ComponentShadowState(value.Component, value.Resource);
            return true;
        }

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
                ensuredComponentsByKey[key] = new EnsuredComponentValue(replacementComponent, resource);
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
