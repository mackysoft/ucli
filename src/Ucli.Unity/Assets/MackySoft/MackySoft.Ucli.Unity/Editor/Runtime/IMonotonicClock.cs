using System;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Provides elapsed process time that is unaffected by UTC clock changes. </summary>
    internal interface IMonotonicClock
    {
        /// <summary> Gets elapsed process time from this clock's origin. </summary>
        TimeSpan Elapsed { get; }
    }
}
