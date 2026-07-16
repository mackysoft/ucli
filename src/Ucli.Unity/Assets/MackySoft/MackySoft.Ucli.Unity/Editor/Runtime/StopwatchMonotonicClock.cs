using System;
using System.Diagnostics;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Measures monotonic process time with <see cref="Stopwatch" />. </summary>
    internal sealed class StopwatchMonotonicClock : IMonotonicClock
    {
        private readonly Stopwatch elapsedTime = Stopwatch.StartNew();

        /// <inheritdoc />
        public TimeSpan Elapsed => elapsedTime.Elapsed;
    }
}
