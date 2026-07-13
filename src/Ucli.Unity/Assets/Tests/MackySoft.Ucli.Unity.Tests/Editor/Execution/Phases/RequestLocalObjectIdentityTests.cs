using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using NUnit.Framework;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class RequestLocalObjectIdentityTests
    {
        [Test]
        [Category("Size.Small")]
        public void FromGlobalObjectId_WhenValueIsNull_ThrowsArgumentNullException ()
        {
            Assert.Throws<ArgumentNullException>(() => RequestLocalObjectIdentity.FromGlobalObjectId(null!));
        }

        [Test]
        [Category("Size.Small")]
        public void FromGlobalObjectId_WhenCanonicalValuesAreEqual_ReturnsEqualKeys ()
        {
            const string canonicalGlobalObjectId = "GlobalObjectId_V1-2-0123456789abcdef0123456789abcdef-123-0";
            var equivalentGlobalObjectId = GlobalObjectIdTestValues.CreateNonCanonicalIdentifierTypeText(canonicalGlobalObjectId);

            var canonicalIdentity = RequestLocalObjectIdentity.FromGlobalObjectId(new UnityGlobalObjectId(canonicalGlobalObjectId));
            var equivalentIdentity = RequestLocalObjectIdentity.FromGlobalObjectId(new UnityGlobalObjectId(equivalentGlobalObjectId));

            Assert.That(equivalentIdentity, Is.EqualTo(canonicalIdentity));
            Assert.That(equivalentIdentity.GetHashCode(), Is.EqualTo(canonicalIdentity.GetHashCode()));
        }

        [Test]
        [Category("Size.Small")]
        public void FromUnityObject_WhenObjectIsTransient_UsesReferenceIdentity ()
        {
            var first = ScriptableObject.CreateInstance<RequestLocalObjectIdentityTestAsset>();
            var second = ScriptableObject.CreateInstance<RequestLocalObjectIdentityTestAsset>();
            try
            {
                var firstIdentity = RequestLocalObjectIdentity.FromUnityObject(first);
                var sameFirstIdentity = RequestLocalObjectIdentity.FromUnityObject(first);
                var secondIdentity = RequestLocalObjectIdentity.FromUnityObject(second);

                Assert.That(firstIdentity.TryGetStableGlobalObjectId(out _), Is.False);
                Assert.That(sameFirstIdentity, Is.EqualTo(firstIdentity));
                Assert.That(secondIdentity, Is.Not.EqualTo(firstIdentity));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void FromUnityObject_WhenTransientObjectIsDestroyed_PreservesDictionaryIdentity ()
        {
            var unityObject = ScriptableObject.CreateInstance<RequestLocalObjectIdentityTestAsset>();
            var key = RequestLocalObjectIdentity.FromUnityObject(unityObject);
            var copiedKey = key;
            var hashCode = key.GetHashCode();
            var valuesByKey = new Dictionary<RequestLocalObjectIdentity, string>
            {
                [key] = "value",
            };

            UnityEngine.Object.DestroyImmediate(unityObject);

            Assert.That(copiedKey, Is.EqualTo(key));
            Assert.That(key.GetHashCode(), Is.EqualTo(hashCode));
            Assert.That(valuesByKey.TryGetValue(key, out var value), Is.True);
            Assert.That(value, Is.EqualTo("value"));
            Assert.That(key.TryGetTransientUnityObject(out _), Is.False);
        }

        private sealed class RequestLocalObjectIdentityTestAsset : ScriptableObject
        {
        }
    }
}
