using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Text;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks plan-time alias bindings independently from other temporary execution state. </summary>
    internal sealed class TemporaryAliasRegistry
    {
        private readonly Dictionary<string, TemporaryAliasValue> valuesByAlias =
            new Dictionary<string, TemporaryAliasValue>(StringComparer.Ordinal);

        public void Set (
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

            valuesByAlias[alias] = new TemporaryAliasValue(unityObject, resource, sourceGlobalObjectId);
        }

        public bool TryGetState (
            string alias,
            out TemporaryAliasState state)
        {
            state = default;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            if (!valuesByAlias.TryGetValue(alias, out var value))
            {
                return false;
            }

            if (value.UnityObject == null)
            {
                valuesByAlias.Remove(alias);
                return false;
            }

            state = new TemporaryAliasState(value.UnityObject, value.Resource, value.SourceGlobalObjectId);
            return true;
        }

        public void SynchronizeBySourceGlobalObjectId (
            string sourceGlobalObjectId,
            UnityEngine.Object unityObject,
            OperationResource resource)
        {
            if (string.IsNullOrWhiteSpace(sourceGlobalObjectId))
            {
                throw new ArgumentException("Source GlobalObjectId must not be null, empty, or whitespace.", nameof(sourceGlobalObjectId));
            }

            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            ValidateResource(resource, nameof(resource));

            List<string>? aliasesToSynchronize = null;
            foreach (var pair in valuesByAlias)
            {
                if (!string.Equals(pair.Value.SourceGlobalObjectId, sourceGlobalObjectId, StringComparison.Ordinal))
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
                valuesByAlias[alias] = new TemporaryAliasValue(unityObject, resource, sourceGlobalObjectId);
            }
        }

        public void ReplaceTrackedObject (
            UnityEngine.Object sourceUnityObject,
            UnityEngine.Object replacementUnityObject,
            OperationResource resource)
        {
            if (sourceUnityObject == null)
            {
                throw new ArgumentNullException(nameof(sourceUnityObject));
            }

            if (replacementUnityObject == null)
            {
                throw new ArgumentNullException(nameof(replacementUnityObject));
            }

            ValidateResource(resource, nameof(resource));

            List<string>? aliasesToSynchronize = null;
            foreach (var pair in valuesByAlias)
            {
                if (pair.Value.UnityObject != sourceUnityObject)
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
                var sourceGlobalObjectId = valuesByAlias[alias].SourceGlobalObjectId;
                valuesByAlias[alias] = new TemporaryAliasValue(replacementUnityObject, resource, sourceGlobalObjectId);
            }
        }

        public void Clear ()
        {
            valuesByAlias.Clear();
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

            public UnityEngine.Object UnityObject { get; }

            public OperationResource Resource { get; }

            public string? SourceGlobalObjectId { get; }
        }
    }
}
