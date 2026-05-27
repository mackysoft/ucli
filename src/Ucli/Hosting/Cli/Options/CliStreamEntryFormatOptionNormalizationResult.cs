using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for a stream-entry <c>--format</c> option. </summary>
internal sealed record CliStreamEntryFormatOptionNormalizationResult (
    CliStreamEntryFormat Format,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful normalization result. </summary>
    public static CliStreamEntryFormatOptionNormalizationResult Success (CliStreamEntryFormat format)
    {
        return new CliStreamEntryFormatOptionNormalizationResult(format, Error: null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    public static CliStreamEntryFormatOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new CliStreamEntryFormatOptionNormalizationResult(default, error);
    }
}
