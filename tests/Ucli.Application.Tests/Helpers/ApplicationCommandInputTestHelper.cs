using System.Globalization;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests.Helpers;

internal static class ApplicationCommandInputTestHelper
{
    public static UnityExecutionMode? NormalizeMode (string? value)
    {
        return value switch
        {
            null => null,
            "auto" => UnityExecutionMode.Auto,
            "daemon" => UnityExecutionMode.Daemon,
            "oneshot" => UnityExecutionMode.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported test execution mode."),
        };
    }

    public static int? NormalizeTimeout (string? value)
    {
        return value is null
            ? null
            : int.Parse(value, CultureInfo.InvariantCulture);
    }

    public static ReadIndexMode? NormalizeReadIndexMode (string? value)
    {
        return value switch
        {
            null => null,
            ReadIndexModeValues.Disabled => ReadIndexMode.Disabled,
            ReadIndexModeValues.AllowStale => ReadIndexMode.AllowStale,
            ReadIndexModeValues.RequireFresh => ReadIndexMode.RequireFresh,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported test readIndex mode."),
        };
    }

    public static TestRunPlatform? NormalizeTestPlatform (string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!TestRunPlatformCodec.TryParse(value, out var testPlatform))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported test platform.");
        }

        return testPlatform;
    }
}
