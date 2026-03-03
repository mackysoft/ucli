using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Sandbox.Tests
{
    public sealed class SandboxSmokeTests
    {
        [Test]
        [Category("Size.Small")]
        public void BasicAssertion_Passes ()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
        }
    }
}
