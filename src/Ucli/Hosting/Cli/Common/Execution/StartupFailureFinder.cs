using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Finds optional Unity startup failure details in application failures. </summary>
internal static class StartupFailureFinder
{
    /// <summary> Gets the first structured startup failure from a failure collection. </summary>
    public static StartupFailureDetail? FindInFailures (IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        for (var i = 0; i < failures.Count; i++)
        {
            var startupFailure = failures[i]?.StartupFailure;
            if (startupFailure is not null)
            {
                return startupFailure;
            }
        }

        return null;
    }
}
