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

        /// <summary> Stores or replaces one request-local alias binding. </summary>
        /// <param name="alias"> The validated alias name. </param>
        /// <param name="unityObject"> The live object bound to the alias. </param>
        /// <param name="resource"> The logical owner resource. </param>
        /// <param name="sourceTrackingKey"> The optional semantic source identity used for replacement and deletion synchronization. </param>
        /// <param name="ownerTrackingKey"> The optional semantic owner identity for a component alias. </param>
        public void Set (
            string alias,
            UnityEngine.Object unityObject,
            OperationResource resource,
            RequestLocalObjectIdentity? sourceTrackingKey,
            RequestLocalObjectIdentity? ownerTrackingKey)
        {
            ValidateAlias(alias);
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            ValidateResource(resource, nameof(resource));
            valuesByAlias[alias] = new TemporaryAliasValue(unityObject, resource, sourceTrackingKey, ownerTrackingKey);
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

            state = new TemporaryAliasState(value.UnityObject, value.Resource, value.SourceTrackingKey);
            return true;
        }

        /// <summary> Advances aliases bound to one semantic source identity to a replacement object. </summary>
        /// <param name="sourceTrackingKey"> The source identity whose aliases must advance. </param>
        /// <param name="unityObject"> The replacement live object. </param>
        /// <param name="resource"> The replacement object's logical owner resource. </param>
        public void SynchronizeBySourceTrackingKey (
            RequestLocalObjectIdentity sourceTrackingKey,
            UnityEngine.Object unityObject,
            OperationResource resource)
        {
            if (sourceTrackingKey == null)
            {
                throw new ArgumentNullException(nameof(sourceTrackingKey));
            }

            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            ValidateResource(resource, nameof(resource));

            List<string>? aliasesToSynchronize = null;
            foreach (var pair in valuesByAlias)
            {
                if (pair.Value.SourceTrackingKey == null
                    || !pair.Value.SourceTrackingKey.Equals(sourceTrackingKey))
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
                var existingValue = valuesByAlias[alias];
                valuesByAlias[alias] = new TemporaryAliasValue(
                    unityObject,
                    resource,
                    sourceTrackingKey,
                    existingValue.OwnerTrackingKey);
            }
        }

        /// <summary> Removes component aliases owned by one deleted GameObject or bound to one of its removed component identities. </summary>
        /// <param name="ownerTrackingKey"> The validated identity of the deleted owner GameObject. </param>
        /// <param name="sourceTrackingKeys"> The non-null set of removed component identities. </param>
        public void RemoveComponentAliases (
            RequestLocalObjectIdentity ownerTrackingKey,
            ISet<RequestLocalObjectIdentity> sourceTrackingKeys)
        {
            if (ownerTrackingKey == null)
            {
                throw new ArgumentNullException(nameof(ownerTrackingKey));
            }

            if (sourceTrackingKeys == null)
            {
                throw new ArgumentNullException(nameof(sourceTrackingKeys));
            }

            List<string>? aliasesToRemove = null;
            foreach (var pair in valuesByAlias)
            {
                var belongsToDeletedOwner = pair.Value.OwnerTrackingKey != null
                    && pair.Value.OwnerTrackingKey.Equals(ownerTrackingKey);
                var hasRemovedSource = pair.Value.SourceTrackingKey != null
                    && sourceTrackingKeys.Contains(pair.Value.SourceTrackingKey);
                if (!belongsToDeletedOwner && !hasRemovedSource)
                {
                    continue;
                }

                aliasesToRemove ??= new List<string>();
                aliasesToRemove.Add(pair.Key);
            }

            if (aliasesToRemove == null)
            {
                return;
            }

            for (var i = 0; i < aliasesToRemove.Count; i++)
            {
                valuesByAlias.Remove(aliasesToRemove[i]);
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
                var existingValue = valuesByAlias[alias];
                valuesByAlias[alias] = new TemporaryAliasValue(
                    replacementUnityObject,
                    resource,
                    existingValue.SourceTrackingKey,
                    existingValue.OwnerTrackingKey);
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
                RequestLocalObjectIdentity? sourceTrackingKey,
                RequestLocalObjectIdentity? ownerTrackingKey)
            {
                UnityObject = unityObject;
                Resource = resource;
                SourceTrackingKey = sourceTrackingKey;
                OwnerTrackingKey = ownerTrackingKey;
            }

            public UnityEngine.Object UnityObject { get; }

            public OperationResource Resource { get; }

            public RequestLocalObjectIdentity? SourceTrackingKey { get; }

            public RequestLocalObjectIdentity? OwnerTrackingKey { get; }
        }

        internal readonly struct TemporaryAliasState
        {
            public TemporaryAliasState (
                UnityEngine.Object unityObject,
                OperationResource resource,
                RequestLocalObjectIdentity? sourceTrackingKey)
            {
                UnityObject = unityObject;
                Resource = resource;
                SourceTrackingKey = sourceTrackingKey;
            }

            public UnityEngine.Object UnityObject { get; }

            public OperationResource Resource { get; }

            public RequestLocalObjectIdentity? SourceTrackingKey { get; }
        }
    }
}
