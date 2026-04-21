using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorDomainReloadGenerationStoreTests
    {
        [SetUp]
        public void SetUp ()
        {
            UnityEditorDomainReloadGenerationStore.SetPersistedValue(0);
        }

        [TearDown]
        public void TearDown ()
        {
            UnityEditorDomainReloadGenerationStore.SetPersistedValue(0);
        }

        [Test]
        [Category("Size.Small")]
        public void Restore_WhenNoGenerationWasPersisted_ReturnsZero ()
        {
            var actual = UnityEditorDomainReloadGenerationStore.Restore();

            Assert.That(actual, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void Advance_PersistsIncrementedGeneration ()
        {
            var advanced = UnityEditorDomainReloadGenerationStore.Advance();
            var restored = UnityEditorDomainReloadGenerationStore.Restore();

            Assert.That(advanced, Is.EqualTo(1));
            Assert.That(restored, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public void Advance_UsesPersistedGenerationWhenItIsHigherThanCurrentValue ()
        {
            UnityEditorDomainReloadGenerationStore.SetPersistedValue(3);

            var actual = UnityEditorDomainReloadGenerationStore.Advance(1);

            Assert.That(actual, Is.EqualTo(4));
            Assert.That(UnityEditorDomainReloadGenerationStore.Restore(), Is.EqualTo(4));
        }
    }
}
