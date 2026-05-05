using System.Globalization;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests.Helpers;

internal static class ApplicationCommandInputTestHelper
{
    public static UnityExecutionMode? NormalizeMode (string? value)
    {
        return value switch
        {
            null => null,
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
}
