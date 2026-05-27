using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes stream-entry <c>--format</c> options into typed rendering formats. </summary>
internal static class CliStreamEntryFormatOptionNormalizer
{
    /// <summary> Normalizes one optional stream-entry format value. </summary>
    public static CliStreamEntryFormatOptionNormalizationResult Normalize (string? format)
    {
        return CliStreamEntryFormatCodec.TryParse(format, out var parsedFormat, out var errorMessage)
            ? CliStreamEntryFormatOptionNormalizationResult.Success(parsedFormat)
            : CliStreamEntryFormatOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(errorMessage!));
    }
}
