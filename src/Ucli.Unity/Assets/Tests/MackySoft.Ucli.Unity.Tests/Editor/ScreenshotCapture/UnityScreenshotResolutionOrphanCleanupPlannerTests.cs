using System;
using MackySoft.Ucli.Unity.ScreenshotCapture;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotResolutionOrphanCleanupPlannerTests
    {
        private const string OwnedLabel = "__ucli_screenshot_temp__0123456789abcdef";

        [Test]
        [Category("Size.Small")]
        public void CreatePlan_WithExactOwnedEntry_RemovesOnlyOwnedIndex ()
        {
            var plan = CreatePlan(
                selectedIndex: 0,
                entries: new[]
                {
                    Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                    Entry(1, "User 1280x720", "FixedResolution", 1280, 720, isCustom: true),
                    Entry(2, OwnedLabel, "FixedResolution", 1920, 1080, isCustom: true),
                });

            Assert.That(plan.IsSuccess, Is.True, plan.ErrorMessage);
            Assert.That(plan.RemovalIndices, Is.EqualTo(new[] { 2 }));
            Assert.That(plan.RegistryLabelsToClear, Is.EqualTo(new[] { OwnedLabel }));
        }

        [Test]
        [Category("Size.Small")]
        public void CreatePlan_WithUnregisteredPrefixedEntry_FailsWithoutRemoval ()
        {
            var plan = UnityScreenshotResolutionOrphanCleanupPlanner.CreatePlan(
                Array.Empty<UnityScreenshotResolutionLeaseRegistry.OwnedResolution>(),
                "Standalone",
                selectedIndex: 0,
                new[]
                {
                    Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                    Entry(1, OwnedLabel, "FixedResolution", 1920, 1080, isCustom: true),
                });

            Assert.That(plan.IsSuccess, Is.False);
            Assert.That(plan.RemovalIndices, Is.Empty);
        }

        [TestCase("AspectRatio", 1920, 1080, true)]
        [TestCase("FixedResolution", 1280, 1080, true)]
        [TestCase("FixedResolution", 1920, 720, true)]
        [TestCase("FixedResolution", 1920, 1080, false)]
        [Category("Size.Small")]
        public void CreatePlan_WhenDescriptorDoesNotMatchMarker_FailsWithoutRemoval (
            string sizeType,
            int width,
            int height,
            bool isCustom)
        {
            var plan = CreatePlan(
                selectedIndex: 0,
                entries: new[]
                {
                    Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                    Entry(1, OwnedLabel, sizeType, width, height, isCustom),
                });

            Assert.That(plan.IsSuccess, Is.False);
            Assert.That(plan.RemovalIndices, Is.Empty);
        }

        [TestCase(1)]
        [TestCase(2)]
        [Category("Size.Small")]
        public void CreatePlan_WhenRemovalCouldChangeSelection_FailsWithoutRemoval (int selectedIndex)
        {
            var plan = CreatePlan(
                selectedIndex,
                new[]
                {
                    Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                    Entry(1, OwnedLabel, "FixedResolution", 1920, 1080, isCustom: true),
                    Entry(2, "User 1280x720", "FixedResolution", 1280, 720, isCustom: true),
                });

            Assert.That(plan.IsSuccess, Is.False);
            Assert.That(plan.RemovalIndices, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void CreatePlan_WhenCurrentSelectionIsUnavailable_FailsWithoutRemoval ()
        {
            var plan = CreatePlan(
                selectedIndex: null,
                entries: new[]
                {
                    Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                    Entry(1, OwnedLabel, "FixedResolution", 1920, 1080, isCustom: true),
                });

            Assert.That(plan.IsSuccess, Is.False);
            Assert.That(plan.RemovalIndices, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void CreatePlan_WhenMarkerBelongsToAnotherGroup_FailsWithoutRemoval ()
        {
            var plan = UnityScreenshotResolutionOrphanCleanupPlanner.CreatePlan(
                new[] { Owned(groupType: "Android") },
                "Standalone",
                selectedIndex: 0,
                new[]
                {
                    Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                    Entry(1, OwnedLabel, "FixedResolution", 1920, 1080, isCustom: true),
                });

            Assert.That(plan.IsSuccess, Is.False);
            Assert.That(plan.RemovalIndices, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void CreatePlan_WithDuplicateOwnershipMarker_FailsInsteadOfThrowing ()
        {
            UnityScreenshotResolutionOrphanCleanupPlanner.CleanupPlan plan = null;

            Assert.DoesNotThrow(() =>
            {
                plan = UnityScreenshotResolutionOrphanCleanupPlanner.CreatePlan(
                    new[] { Owned(), Owned() },
                    "Standalone",
                    selectedIndex: 0,
                    new[]
                    {
                        Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                        Entry(1, OwnedLabel, "FixedResolution", 1920, 1080, isCustom: true),
                    });
            });
            Assert.That(plan.IsSuccess, Is.False);
            Assert.That(plan.RemovalIndices, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void CreatePlan_WithStaleMarkerOnly_ClearsMarkerWithoutMutation ()
        {
            var plan = CreatePlan(
                selectedIndex: null,
                entries: new[]
                {
                    Entry(0, "Free Aspect", "AspectRatio", 0, 0, isCustom: false),
                });

            Assert.That(plan.IsSuccess, Is.True, plan.ErrorMessage);
            Assert.That(plan.RemovalIndices, Is.Empty);
            Assert.That(plan.RegistryLabelsToClear, Is.EqualTo(new[] { OwnedLabel }));
        }

        private static UnityScreenshotResolutionOrphanCleanupPlanner.CleanupPlan CreatePlan (
            int? selectedIndex,
            UnityScreenshotResolutionOrphanCleanupPlanner.GroupEntry[] entries)
        {
            return UnityScreenshotResolutionOrphanCleanupPlanner.CreatePlan(
                new[] { Owned() },
                "Standalone",
                selectedIndex,
                entries);
        }

        private static UnityScreenshotResolutionLeaseRegistry.OwnedResolution Owned (
            string groupType = "Standalone")
        {
            return new UnityScreenshotResolutionLeaseRegistry.OwnedResolution(
                OwnedLabel,
                Width: 1920,
                Height: 1080,
                GroupType: groupType,
                OriginalIndex: 0);
        }

        private static UnityScreenshotResolutionOrphanCleanupPlanner.GroupEntry Entry (
            int index,
            string label,
            string sizeType,
            int width,
            int height,
            bool isCustom)
        {
            return new UnityScreenshotResolutionOrphanCleanupPlanner.GroupEntry(
                index,
                label,
                sizeType,
                width,
                height,
                isCustom);
        }
    }
}
