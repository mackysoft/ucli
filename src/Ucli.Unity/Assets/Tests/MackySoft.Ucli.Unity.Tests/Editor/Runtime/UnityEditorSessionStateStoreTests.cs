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
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance");

            var actual = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();

            Assert.That(actual, Is.EqualTo("editor-instance"));
        }
    }
}
