using System;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorSessionStateStoreTests
    {
        [SetUp]
        public void SetUp ()
        {
            UnityEditorSessionStateStore.SetDomainReloadGenerationForTests(0);
            UnityEditorSessionStateStore.SetPlayModeGenerationForTests(0);
            UnityEditorSessionStateStore.SetAssetRefreshGenerationForTests(0);
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(null);
        }

        [TearDown]
        public void TearDown ()
        {
            UnityEditorSessionStateStore.SetDomainReloadGenerationForTests(0);
            UnityEditorSessionStateStore.SetPlayModeGenerationForTests(0);
            UnityEditorSessionStateStore.SetAssetRefreshGenerationForTests(0);
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(null);
        }

        [Test]
        [Category("Size.Small")]
        public void RestoreDomainReloadGeneration_WhenNoGenerationWasPersisted_ReturnsZero ()
        {
            var actual = UnityEditorSessionStateStore.RestoreDomainReloadGeneration();

            Assert.That(actual, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void AdvanceDomainReloadGeneration_PersistsIncrementedGeneration ()
        {
            var advanced = UnityEditorSessionStateStore.AdvanceDomainReloadGeneration();
            var restored = UnityEditorSessionStateStore.RestoreDomainReloadGeneration();

            Assert.That(advanced, Is.EqualTo(1));
            Assert.That(restored, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public void AdvanceDomainReloadGeneration_UsesPersistedGenerationWhenItIsHigherThanCurrentValue ()
        {
            UnityEditorSessionStateStore.SetDomainReloadGenerationForTests(3);

            var actual = UnityEditorSessionStateStore.AdvanceDomainReloadGeneration(1);

            Assert.That(actual, Is.EqualTo(4));
            Assert.That(UnityEditorSessionStateStore.RestoreDomainReloadGeneration(), Is.EqualTo(4));
        }

        [Test]
        [Category("Size.Small")]
        public void RestoreAssetRefreshGeneration_WhenNoGenerationWasPersisted_ReturnsZero ()
        {
            var actual = UnityEditorSessionStateStore.RestoreAssetRefreshGeneration();

            Assert.That(actual, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void AdvanceAssetRefreshGeneration_PersistsIncrementedGeneration ()
        {
            var advanced = UnityEditorSessionStateStore.AdvanceAssetRefreshGeneration();
            var restored = UnityEditorSessionStateStore.RestoreAssetRefreshGeneration();

            Assert.That(advanced, Is.EqualTo(1));
            Assert.That(restored, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public void GetOrCreateEditorInstanceId_WhenValueExists_ReusesStoredValue ()
        {
            var expected = Guid.NewGuid().ToString("N");
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(expected);

            var actual = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();

            Assert.That(actual.ToString("N"), Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("editor-instance")]
        [TestCase("00000000000000000000000000000000")]
        [TestCase("11111111-1111-1111-1111-111111111111")]
        [TestCase(" 11111111111111111111111111111111 ")]
        [TestCase("1111111111111111111111111111111")]
        [Category("Size.Small")]
        public void GetOrCreateEditorInstanceId_WhenStoredValueIsInvalid_ReplacesItWithNonEmptyGuid (
            string editorInstanceId)
        {
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(editorInstanceId);

            var actual = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();
            var repeated = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();

            Assert.That(actual, Is.Not.EqualTo(Guid.Empty));
            Assert.That(repeated, Is.EqualTo(actual));
        }
    }
}
