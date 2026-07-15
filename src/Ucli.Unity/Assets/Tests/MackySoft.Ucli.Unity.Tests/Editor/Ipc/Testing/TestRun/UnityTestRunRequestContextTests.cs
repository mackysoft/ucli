using System;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityTestRunRequestContextTests
    {
        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenRunIdIsEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() => new UnityTestRunRequestContext(
                Guid.Empty,
                null!,
                default,
                null,
                null,
                null!,
                null!,
                null!,
                null!,
                null!));

            Assert.That(exception.ParamName, Is.EqualTo("runId"));
        }
    }
}
