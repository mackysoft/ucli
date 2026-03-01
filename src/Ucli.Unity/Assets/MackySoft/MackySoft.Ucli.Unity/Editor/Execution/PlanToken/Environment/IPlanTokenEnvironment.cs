using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Captures runtime values required for plan-token generation and validation. </summary>
    internal interface IPlanTokenEnvironment
    {
        /// <summary> Captures one runtime environment snapshot. </summary>
        /// <returns> The captured snapshot. </returns>
        PlanTokenEnvironmentSnapshot Capture ();

        /// <summary> Gets the current UTC time. </summary>
        DateTimeOffset UtcNow { get; }
    }
}
