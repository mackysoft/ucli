using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Projects optional Unity startup failure details into command payload dictionaries. </summary>
internal static class StartupFailurePayloadProjector
{
    /// <summary> Appends the first startup failure detail from the supplied failures when one exists. </summary>
    public static void AppendFromFailures (
        IDictionary<string, object?> payload,
        IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(failures);

        for (var i = 0; i < failures.Count; i++)
        {
            var startupFailure = failures[i]?.StartupFailure;
            if (startupFailure is not null)
            {
                Append(payload, startupFailure);
                return;
            }
        }
    }

    /// <summary> Appends one startup failure detail to a command payload dictionary. </summary>
    public static void Append (
        IDictionary<string, object?> payload,
        StartupFailureDetail? startupFailure)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (startupFailure is null)
        {
            return;
        }

        if (startupFailure.Startup is not null)
        {
            payload["startup"] = startupFailure.Startup;
        }

        if (startupFailure.Diagnosis is not null)
        {
            payload["diagnosis"] = startupFailure.Diagnosis;
        }

        if (!string.IsNullOrWhiteSpace(startupFailure.RetryDisposition))
        {
            payload["retryDisposition"] = startupFailure.RetryDisposition;
        }

        payload["safeToRetryImmediately"] = startupFailure.SafeToRetryImmediately;
    }
}
