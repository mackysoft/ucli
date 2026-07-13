using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using NUnit.Framework;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ComponentSandboxRegistryTests
    {
        [Test]
        [Category("Size.Small")]
        public void ReplaceTrackedTemporaryComponent_WhenSourcesWereDestroyed_ReplacesOnlyReferenceEqualState ()
        {
            var registry = new ComponentSandboxRegistry();
            var aliases = new TemporaryAliasRegistry();
            var ownerA = new GameObject("OwnerA");
            var ownerB = new GameObject("OwnerB");
            var replacementOwner = new GameObject("ReplacementOwner");
            try
            {
                var sourceA = ownerA.AddComponent<CompOperationTestComponent>();
                var sourceB = ownerB.AddComponent<CompOperationTestComponent>();
                var replacement = replacementOwner.AddComponent<CompOperationTestComponent>();
                var ownerKeyA = RequestLocalObjectIdentity.FromUnityObject(ownerA);
                var ownerKeyB = RequestLocalObjectIdentity.FromUnityObject(ownerB);
                var sourceKeyA = RequestLocalObjectIdentity.FromUnityObject(sourceA);
                var sourceKeyB = RequestLocalObjectIdentity.FromUnityObject(sourceB);
                var resource = new OperationResource(OperationTouchKind.Scene, "Assets/Scene.unity");

                registry.SetEnsuredComponent(ownerKeyA, typeof(CompOperationTestComponent), sourceA, ownerA, resource);
                registry.SetEnsuredComponent(ownerKeyB, typeof(CompOperationTestComponent), sourceB, ownerB, resource);
                aliases.Set("component-a", sourceA, resource, sourceKeyA, ownerKeyA);
                aliases.Set("component-b", sourceB, resource, sourceKeyB, ownerKeyB);
                Object.DestroyImmediate(sourceA);
                Object.DestroyImmediate(sourceB);

                registry.ReplaceTrackedTemporaryComponent(sourceA, replacement, resource, aliases);

                Assert.That(
                    registry.TryGetEnsuredComponentState(ownerKeyA, typeof(CompOperationTestComponent), out var ensuredA),
                    Is.True);
                Assert.That(ensuredA.Component, Is.SameAs(replacement));
                Assert.That(
                    registry.TryGetEnsuredComponentState(ownerKeyB, typeof(CompOperationTestComponent), out _),
                    Is.False);
                Assert.That(aliases.TryGetState("component-a", out var aliasA), Is.True);
                Assert.That(aliasA.UnityObject, Is.SameAs(replacement));
                Assert.That(aliases.TryGetState("component-b", out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(ownerA);
                Object.DestroyImmediate(ownerB);
                Object.DestroyImmediate(replacementOwner);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TrackedComponentLookups_WhenShadowOwnerWasDestroyed_DoNotExposeStaleState ()
        {
            var registry = new ComponentSandboxRegistry();
            var aliases = new TemporaryAliasRegistry();
            var owner = new GameObject("Owner");
            var sourceHost = new GameObject("Source");
            var shadowHost = new GameObject("Shadow");
            try
            {
                var source = sourceHost.AddComponent<CompOperationTestComponent>();
                var shadow = shadowHost.AddComponent<CompOperationTestComponent>();
                var sourceKey = RequestLocalObjectIdentity.FromUnityObject(source);
                var ownerKey = RequestLocalObjectIdentity.FromUnityObject(owner);
                var resource = new OperationResource(OperationTouchKind.Scene, "Assets/Scene.unity");
                registry.SetComponentShadow(sourceKey, shadow, source, owner, ownerKey, resource, aliases);

                Object.DestroyImmediate(owner);

                Assert.That(registry.TryResolveTrackedComponentResource(shadow, out _), Is.False);
                Assert.That(registry.TryResolveTrackedComponentTargetKey(shadow, out _), Is.False);
                Assert.That(registry.TryResolveTrackedComponentOwnerKey(shadow, out _), Is.False);
                Assert.That(registry.TryResolveTrackedComponentOwnerGameObject(shadow, out _), Is.False);
                Assert.That(registry.TryGetComponentShadowState(sourceKey, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(sourceHost);
                Object.DestroyImmediate(shadowHost);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void EnsuredComponentLookups_WhenSemanticOwnerWasDestroyed_DoNotExposeStaleState ()
        {
            var registry = new ComponentSandboxRegistry();
            var owner = new GameObject("Owner");
            var componentHost = new GameObject("ComponentHost");
            try
            {
                var component = componentHost.AddComponent<CompOperationTestComponent>();
                var ownerKey = RequestLocalObjectIdentity.FromUnityObject(owner);
                var resource = new OperationResource(OperationTouchKind.Scene, "Assets/Scene.unity");
                registry.SetEnsuredComponent(
                    ownerKey,
                    typeof(CompOperationTestComponent),
                    component,
                    owner,
                    resource);

                Object.DestroyImmediate(owner);

                Assert.That(registry.TryResolveTrackedComponentResource(component, out _), Is.False);
                Assert.That(registry.TryResolveTrackedComponentTargetKey(component, out _), Is.False);
                Assert.That(registry.TryResolveTrackedComponentOwnerKey(component, out _), Is.False);
                Assert.That(registry.TryResolveTrackedComponentOwnerGameObject(component, out _), Is.False);
                var collectedStates = new List<ComponentSandboxRegistry.EnsuredComponentState>();
                registry.CollectEnsuredComponentStates(ownerKey, collectedStates);
                Assert.That(collectedStates, Is.Empty);
                Assert.That(
                    registry.TryGetEnsuredComponentState(ownerKey, typeof(CompOperationTestComponent), out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(componentHost);
            }
        }
    }
}
