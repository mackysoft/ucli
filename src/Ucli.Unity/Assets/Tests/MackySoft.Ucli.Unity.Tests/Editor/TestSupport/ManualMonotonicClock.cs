using System;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Tests
{
    internal sealed class ManualMonotonicClock : IMonotonicClock
    {
        public TimeSpan Elapsed { get; private set; }

        public void Advance (TimeSpan elapsedTime)
        {
            if (elapsedTime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime), elapsedTime, "Elapsed time must not be negative.");
            }

            Elapsed += elapsedTime;
        }
    }
}
