using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Represents one successful or failed test-run configuration resolution. </summary>
internal abstract record TestRunConfigurationResolutionResult
{
    private TestRunConfigurationResolutionResult ()
    {
    }

    /// <summary> Creates a successful configuration resolution result. </summary>
    public static Succeeded Success (ResolvedTestRunConfiguration configuration)
    {
        return new Succeeded(configuration);
    }

    /// <summary> Creates a failed configuration resolution result. </summary>
    public static Failed Failure (IReadOnlyList<ExecutionError> errors)
    {
        return new Failed(errors);
    }

    /// <summary> Represents one successfully resolved configuration. </summary>
    internal sealed record Succeeded : TestRunConfigurationResolutionResult
    {
        internal Succeeded (ResolvedTestRunConfiguration configuration)
        {
            Configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary> Gets the resolved test-run configuration. </summary>
        public ResolvedTestRunConfiguration Configuration { get; }
    }

    /// <summary> Represents one failed configuration resolution. </summary>
    internal sealed record Failed : TestRunConfigurationResolutionResult
    {
        internal Failed (IReadOnlyList<ExecutionError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (errors.Count == 0)
            {
                throw new ArgumentException(
                    "A failed configuration resolution must contain at least one error.",
                    nameof(errors));
            }

            var copiedErrors = new ExecutionError[errors.Count];
            for (var i = 0; i < errors.Count; i++)
            {
                copiedErrors[i] = errors[i]
                    ?? throw new ArgumentException(
                        "Configuration resolution errors must not contain null entries.",
                        nameof(errors));
            }

            Errors = Array.AsReadOnly(copiedErrors);
        }

        /// <summary> Gets the errors that prevented configuration resolution. </summary>
        public IReadOnlyList<ExecutionError> Errors { get; }
    }
}
