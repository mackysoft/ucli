using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Hosting.Cli.Options;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Tests.Helpers.Cli;

internal static class CommandOptionNormalizationTestHelper
{
    public static UnityExecutionMode? NormalizeMode (string? value)
    {
        var result = ExecutionModeOptionNormalizer.Normalize(value);
        if (!result.IsSuccess)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, result.Error!.Message);
        }

        return result.Mode;
    }

    public static int? NormalizeTimeout (string? value)
    {
        var result = TimeoutOptionNormalizer.Normalize(value);
        if (!result.IsSuccess)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, result.Error!.Message);
        }

        return result.TimeoutMilliseconds;
    }

    public static ReadIndexMode? NormalizeReadIndexMode (string? value)
    {
        var result = ReadIndexModeOptionNormalizer.Normalize(value);
        if (!result.IsSuccess)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, result.Error!.Message);
        }

        return result.Mode;
    }

    public static TestRunPlatform? NormalizeTestPlatform (string? value)
    {
        var result = TestRunPlatformOptionNormalizer.Normalize(value);
        if (!result.IsSuccess)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, result.Error!.Message);
        }

        return result.TestPlatform;
    }
}
