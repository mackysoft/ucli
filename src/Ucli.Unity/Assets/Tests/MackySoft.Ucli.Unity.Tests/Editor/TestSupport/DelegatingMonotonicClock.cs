using System;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Exposes a deterministic monotonic-clock observation for concurrency tests. </summary>
    internal sealed class DelegatingMonotonicClock : IMonotonicClock
    {
        private readonly Func<TimeSpan> getElapsed;

        public DelegatingMonotonicClock (Func<TimeSpan> getElapsed)
        {
            this.getElapsed = getElapsed ?? throw new ArgumentNullException(nameof(getElapsed));
        }

        public TimeSpan Elapsed => getElapsed();
    }
}
