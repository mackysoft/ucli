using System.Text.Json;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunCliOutputContractTestSupport
{
    public const string GoldenDirectory = "build-run";

    public static BuildRunGoldenCase[] GoldenCases { get; } =
    [
        new("success.json", "success"),
        new("build-report-failed.json", "build-report-failed"),
        new("invalid-profile.json", "invalid-profile"),
        new("unsupported-buildTarget.json", "unsupported-buildTarget"),
        new("dirty-scene.json", "dirty-scene"),
        new("buildTarget-module-missing.json", "buildTarget-module-missing"),
        new("artifact-write-failed.json", "artifact-write-failed"),
        new("output-manifest-failed.json", "output-manifest-failed"),
    ];

    public static CommandResult CreateCommandResult (string caseName)
    {
        return BuildRunCliOutputFixtureFactory.CreateCommandResult(caseName);
    }

    public static JsonDocument ReadGoldenDocument (string fileName)
    {
        return CliOutputGoldenFiles.ReadJsonDocument(GoldenDirectory, fileName);
    }

    public static JsonElement ReadGoldenPayload (string fileName)
    {
        using var document = ReadGoldenDocument(fileName);
        return document.RootElement.GetProperty("payload").Clone();
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

    internal readonly record struct BuildRunGoldenCase (
        string FileName,
        string CaseName);
}
