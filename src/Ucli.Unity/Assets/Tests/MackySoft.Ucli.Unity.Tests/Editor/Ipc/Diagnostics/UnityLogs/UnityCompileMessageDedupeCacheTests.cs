using System;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityCompileMessageDedupeCacheTests
    {
        [Test]
        [Category("Size.Small")]
        public void ContainsRecent_UsesMonotonicFiveSecondLifetimeFromLatestRegistration ()
        {
            var monotonicClock = new ManualMonotonicClock();
            var cache = new UnityCompileMessageDedupeCache(monotonicClock);

            cache.Register("compile error");
            monotonicClock.Advance(TimeSpan.FromMilliseconds(4999));
            Assert.That(cache.ContainsRecent("compile error"), Is.True);

            cache.Register("compile error");
            monotonicClock.Advance(TimeSpan.FromSeconds(5));
            Assert.That(cache.ContainsRecent("compile error"), Is.False);
        }
    }
}
