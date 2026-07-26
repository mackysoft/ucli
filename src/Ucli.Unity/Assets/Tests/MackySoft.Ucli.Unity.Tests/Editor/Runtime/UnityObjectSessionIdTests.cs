using System.Collections.Generic;
using System.Globalization;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityObjectSessionIdTests
    {
        [Test]
        [Category("Size.Small")]
        public void Create_ForSameLiveObject_PreservesEqualityHashTextAndDictionaryLookup ()
        {
            var unityObject = new GameObject("Target");

            try
            {
                var firstId = UnityObjectSessionId.Create(unityObject);
                var repeatedId = UnityObjectSessionId.Create(unityObject);
                var owners = new Dictionary<UnityObjectSessionId, string>
                {
                    [firstId] = "first",
                };

                Assert.That(repeatedId, Is.EqualTo(firstId));
                Assert.That(repeatedId.GetHashCode(), Is.EqualTo(firstId.GetHashCode()));
                Assert.That(repeatedId.ToString(), Is.EqualTo(firstId.ToString()));
                Assert.That(owners.TryGetValue(repeatedId, out var owner), Is.True);
                Assert.That(owner, Is.EqualTo("first"));

#if UNITY_6000_5_OR_NEWER
                Assert.That(firstId.ToString(), Is.EqualTo(unityObject.GetEntityId().ToString()));
#else
                Assert.That(
                    firstId.ToString(),
                    Is.EqualTo(unityObject.GetInstanceID().ToString(CultureInfo.InvariantCulture)));
#endif
            }
            finally
            {
                Object.DestroyImmediate(unityObject);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Create_ForDifferentLiveObjects_ProducesDifferentIdentityAndText ()
        {
            var firstObject = new GameObject("First");
            var secondObject = new GameObject("Second");

            try
            {
                var firstId = UnityObjectSessionId.Create(firstObject);
                var secondId = UnityObjectSessionId.Create(secondObject);

                Assert.That(secondId, Is.Not.EqualTo(firstId));
                Assert.That(secondId.ToString(), Is.Not.EqualTo(firstId.ToString()));
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(firstObject);
            }
        }
    }
}
