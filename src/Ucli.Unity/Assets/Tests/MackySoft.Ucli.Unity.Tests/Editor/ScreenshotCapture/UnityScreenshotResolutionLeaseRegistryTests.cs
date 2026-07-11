using System.Linq;
using MackySoft.Ucli.Unity.ScreenshotCapture;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotResolutionLeaseRegistryTests
    {
        [SetUp]
        public void SetUp ()
        {
            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
        }

        [TearDown]
        public void TearDown ()
        {
            UnityScreenshotResolutionLeaseRegistry.ClearForTests();
        }

        [Test]
        [Category("Size.Small")]
        public void CreateLabel_UsesExactOwnershipSyntax ()
        {
            var label = UnityScreenshotResolutionLeaseRegistry.CreateLabel();

            Assert.That(
                UnityScreenshotResolutionLeaseRegistry.IsOwnedLabelSyntax(label),
                Is.True);
            Assert.That(label.Length, Is.EqualTo(40));
        }

        [Test]
        [Category("Size.Small")]
        public void RegisterAndUnregister_RoundTripsExactOwnershipMarker ()
        {
            var owned = new UnityScreenshotResolutionLeaseRegistry.OwnedResolution(
                UnityScreenshotResolutionLeaseRegistry.CreateLabel(),
                Width: 1920,
                Height: 1080,
                GroupType: "Standalone",
                OriginalIndex: 2);

            UnityScreenshotResolutionLeaseRegistry.Register(owned);

            Assert.That(
                UnityScreenshotResolutionLeaseRegistry.TryRead(out var registered, out var readError),
                Is.True,
                readError);
            Assert.That(registered.Single(), Is.EqualTo(owned));
            Assert.That(
                UnityScreenshotResolutionLeaseRegistry.TryUnregister(owned.Label, out var removeError),
                Is.True,
                removeError);
            Assert.That(
                UnityScreenshotResolutionLeaseRegistry.TryRead(out var remaining, out readError),
                Is.True,
                readError);
            Assert.That(remaining, Is.Empty);
        }
    }
}
