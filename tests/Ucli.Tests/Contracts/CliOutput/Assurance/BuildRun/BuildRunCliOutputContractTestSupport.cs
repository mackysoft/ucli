using System.Text.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunCliOutputContractTestSupport
{
    public static CommandResult CreateCommandResult (string caseName)
    {
        return BuildRunCliOutputFixtureFactory.CreateCommandResult(caseName);
    }

    public static JsonDocument CreateDocument (string caseName)
    {
        var result = CreateCommandResult(caseName);
        var json = new CommandResultJsonContractWriter().Write(result);
        return JsonDocument.Parse(json);
    }

    public static string GetClaimStatus (
        JsonElement payload,
        string claimId)
    {
        foreach (var claim in payload.GetProperty("claims").EnumerateArray())
        {
            if (string.Equals(claim.GetProperty("id").GetString(), claimId, StringComparison.Ordinal))
            {
                return claim.GetProperty("status").GetString()!;
            }
        }

        throw new InvalidOperationException($"Claim was not found: {claimId}");
    }

    public static bool IsAbsoluteLikePath (string path)
    {
        return Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("\\", StringComparison.Ordinal)
            || (path.Length >= 3 && IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'));
    }

    private static bool IsAsciiLetter (char value)
    {
        return value is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z');
    }
}
