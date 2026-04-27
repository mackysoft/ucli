using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Normalizes common options shared by typed query commands. </summary>
internal static class QueryCommonOptionsNormalizer
{
    /// <summary> Normalizes common typed-query command options. </summary>
    public static QueryCommonOptionsNormalizationResult Normalize (
        string? projectPath,
        string? mode,
        string? timeout,
        string? readIndexMode,
        bool failFast)
    {
        var normalizedReadIndexModeResult = ReadIndexModeOptionNormalizer.Normalize(readIndexMode);
        if (!normalizedReadIndexModeResult.IsSuccess)
        {
            return QueryCommonOptionsNormalizationResult.Failure(normalizedReadIndexModeResult.Error!);
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            return QueryCommonOptionsNormalizationResult.Failure(normalizedTimeoutResult.Error!);
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            return QueryCommonOptionsNormalizationResult.Failure(normalizedModeResult.Error!);
        }

        return QueryCommonOptionsNormalizationResult.Success(new QueryCommonOptions(
            ProjectPath: projectPath,
            Mode: normalizedModeResult.Mode,
            TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
            ReadIndexMode: normalizedReadIndexModeResult.Mode,
            FailFast: failFast));
    }
}
