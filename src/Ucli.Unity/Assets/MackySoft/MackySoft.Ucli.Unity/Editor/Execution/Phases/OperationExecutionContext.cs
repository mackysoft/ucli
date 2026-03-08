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
            string scenePath)
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

            temporaryAliasesByName[alias] = new TemporaryAliasValue(unityObject, scenePath);
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
            unityObject = null;
            scenePath = string.Empty;
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

        private readonly struct TemporaryAliasValue
        {
            public TemporaryAliasValue (
                UnityEngine.Object unityObject,
                string scenePath)
            {
                UnityObject = unityObject;
                ScenePath = scenePath;
            }

            public UnityEngine.Object UnityObject { get; }

            public string ScenePath { get; }
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
    }
}