using System.Globalization;
using System.Text;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class VerifyScopeDetectorTests
{
    private static readonly IReadOnlyList<VerifyScopeDetectorChangeCase> VerifyScopeDetectorChangeCases =
    [
        new(
            "Directory.Build.props",
            "Directory.Build.props",
            "<Project><PropertyGroup><Version>0.0.0</Version></PropertyGroup></Project>",
            "<Project><PropertyGroup><Version>0.0.1</Version></PropertyGroup></Project>",
            NeedsDotnet: "true",
            NeedsSharedPack: "true",
            NeedsCliPack: "true",
            NeedsUnity: "false",
            NeedsUnityPack: "false",
            NeedsReleasePack: "true"),
        new(
            ".gitattributes",
            ".gitattributes",
            "skills/definitions/** text=auto\n",
            "skills/generated/** text eol=lf\n",
            NeedsDotnet: "true",
            NeedsSharedPack: "true",
            NeedsCliPack: "true",
            NeedsUnity: "true",
            NeedsUnityPack: "true",
            NeedsReleasePack: "true"),
        new(
            "schema artifact",
            "schemas/v1/schema-manifest.json",
            """{"schemaSet":"ucli","packageVersion":"0.0.0"}""",
            """{"schemaSet":"ucli","packageVersion":"0.0.1"}""",
            NeedsDotnet: "true",
            NeedsSharedPack: "false",
            NeedsCliPack: "true",
            NeedsUnity: "false",
            NeedsUnityPack: "false",
            NeedsReleasePack: "true"),
        new(
            "schema generation script",
            "scripts/generate-schemas.sh",
            "echo old\n",
            "echo new\n",
            NeedsDotnet: "true",
            NeedsSharedPack: "false",
            NeedsCliPack: "true",
            NeedsUnity: "false",
            NeedsUnityPack: "false",
            NeedsReleasePack: "true"),
        new(
            "external vocabulary dependency",
            "src/Ucli.Contracts/Ucli.Contracts.csproj",
            """<PackageReference Include="MackySoft.Text.Vocabularies" Version="0.1.0" />""",
            """<PackageReference Include="MackySoft.Text.Vocabularies" Version="0.1.1" />""",
            NeedsDotnet: "true",
            NeedsSharedPack: "true",
            NeedsCliPack: "true",
            NeedsUnity: "true",
            NeedsUnityPack: "false",
            NeedsReleasePack: "false"),
        new(
            "Agent Skills sync workflow",
            ".github/workflows/agent-skills-sync.yaml",
            "name: agent-skills-sync\n",
            "name: agent-skills-sync\n# changed\n",
            NeedsDotnet: "true",
            NeedsSharedPack: "false",
            NeedsCliPack: "false",
            NeedsUnity: "false",
            NeedsUnityPack: "false",
            NeedsReleasePack: "false"),
        new(
            "release publish workflow",
            ".github/workflows/package-publish.yaml",
            "name: package-publish\n",
            "name: package-publish\n# changed\n",
            NeedsDotnet: "true",
            NeedsSharedPack: "true",
            NeedsCliPack: "true",
            NeedsUnity: "false",
            NeedsUnityPack: "true",
            NeedsReleasePack: "true"),
        new(
            "Unity package metadata",
            "src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec",
            "<package><metadata><version>0.0.0</version></metadata></package>",
            "<package><metadata><version>0.0.1</version></metadata></package>",
            NeedsDotnet: "false",
            NeedsSharedPack: "false",
            NeedsCliPack: "false",
            NeedsUnity: "false",
            NeedsUnityPack: "true",
            NeedsReleasePack: "true"),
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Verify_scope_detector_tracks_packaging_related_changes ()
    {
        VerifyScopeDetectorCaseResult[] outputsByCase = await RunVerifyScopeDetectorCasesAsync();

        foreach (VerifyScopeDetectorCaseResult caseResult in outputsByCase)
        {
            VerifyScopeDetectorChangeCase testCase = caseResult.TestCase;
            IReadOnlyDictionary<string, string> outputs = caseResult.Outputs;

            AssertDetectorOutput(outputs, testCase.Name, "needs_dotnet", testCase.NeedsDotnet);
            AssertDetectorOutput(outputs, testCase.Name, "needs_shared_pack", testCase.NeedsSharedPack);
            AssertDetectorOutput(outputs, testCase.Name, "needs_cli_pack", testCase.NeedsCliPack);
            AssertDetectorOutput(outputs, testCase.Name, "needs_unity", testCase.NeedsUnity);
            AssertDetectorOutput(outputs, testCase.Name, "needs_unity_pack", testCase.NeedsUnityPack);
            AssertDetectorOutput(outputs, testCase.Name, "needs_release_pack", testCase.NeedsReleasePack);
        }
    }

    private static async Task<VerifyScopeDetectorCaseResult[]> RunVerifyScopeDetectorCasesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "verify-scope-detector",
            "packaging-related-changes",
            DirectoryCleanupMode.BestEffort);

        await TestProcessRunner.RunRequiredAsync("git", ["init", "--quiet"], scope.FullPath);
        string[] commitShas = await ImportVerifyScopeDetectorHistoryAsync(scope.FullPath);
        var previousSha = commitShas[0];
        var detectorInputs = new List<(VerifyScopeDetectorChangeCase TestCase, string BaseSha, string HeadSha)>();
        for (var i = 0; i < VerifyScopeDetectorChangeCases.Count; i++)
        {
            VerifyScopeDetectorChangeCase testCase = VerifyScopeDetectorChangeCases[i];
            string headSha = commitShas[i + 1];
            detectorInputs.Add((testCase, previousSha, headSha));
            previousSha = headSha;
        }

        string detectorScriptPath = TestShellPaths.ToBashPath(TestRepositoryPaths.GetFullPath("scripts", "detect-verify-scopes.sh"));
        return await RunVerifyScopeDetectorCasesAsync(scope.FullPath, detectorScriptPath, detectorInputs);
    }

    private static async Task<string[]> ImportVerifyScopeDetectorHistoryAsync (string repositoryPath)
    {
        string marksPath = Path.Combine(repositoryPath, "fast-import-marks.txt");
        var importScript = new StringBuilder();
        AppendFastImportCommit(
            importScript,
            mark: 1,
            parentMark: null,
            message: "initial",
            files: VerifyScopeDetectorChangeCases.Select(static testCase => (testCase.RelativePath, testCase.InitialContents)));

        for (var i = 0; i < VerifyScopeDetectorChangeCases.Count; i++)
        {
            VerifyScopeDetectorChangeCase testCase = VerifyScopeDetectorChangeCases[i];
            AppendFastImportCommit(
                importScript,
                mark: i + 2,
                parentMark: i + 1,
                message: "change file",
                files: [(testCase.RelativePath, testCase.ChangedContents)]);
        }

        await TestProcessRunner.RunRequiredAsync(
            "git",
            ["fast-import", "--quiet", "--export-marks=" + marksPath],
            repositoryPath,
            standardInput: importScript.ToString());
        return ParseFastImportMarks(
            await File.ReadAllTextAsync(marksPath),
            VerifyScopeDetectorChangeCases.Count + 1);
    }

    private static void AppendFastImportCommit (
        StringBuilder builder,
        int mark,
        int? parentMark,
        string message,
        IEnumerable<(string RelativePath, string Contents)> files)
    {
        builder.Append("commit refs/heads/main\n");
        builder.Append("mark :").Append(mark).Append('\n');
        builder.Append("committer uCLI Tests <ucli-tests@example.invalid> 1700000000 +0000\n");
        AppendFastImportData(builder, message);
        if (parentMark is int parent)
        {
            builder.Append("from :").Append(parent).Append('\n');
        }

        foreach ((string relativePath, string contents) in files)
        {
            builder.Append("M 100644 inline ").Append(relativePath).Append('\n');
            AppendFastImportData(builder, contents);
        }
    }

    private static void AppendFastImportData (StringBuilder builder, string value)
    {
        builder.Append("data ").Append(Encoding.UTF8.GetByteCount(value)).Append('\n');
        builder.Append(value).Append('\n');
    }

    private static string[] ParseFastImportMarks (string marksContents, int expectedMarkCount)
    {
        string[] commitShas = new string[expectedMarkCount];
        using var reader = new StringReader(marksContents);
        while (reader.ReadLine() is { } line)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[0].StartsWith(':'))
            {
                continue;
            }

            int mark = int.Parse(parts[0][1..], NumberStyles.None, CultureInfo.InvariantCulture);
            if (mark is > 0 && mark <= expectedMarkCount)
            {
                commitShas[mark - 1] = parts[1];
            }
        }

        for (var i = 0; i < commitShas.Length; i++)
        {
            Assert.False(string.IsNullOrWhiteSpace(commitShas[i]), $"fast-import did not export mark :{i + 1}.");
        }

        return commitShas;
    }

    private static async Task<VerifyScopeDetectorCaseResult[]> RunVerifyScopeDetectorCasesAsync (
        string repositoryPath,
        string detectorScriptPath,
        IReadOnlyList<(VerifyScopeDetectorChangeCase TestCase, string BaseSha, string HeadSha)> detectorInputs)
    {
        string outputDirectoryPath = Path.Combine(repositoryPath, "detector-outputs");
        string runnerScriptPath = Path.Combine(repositoryPath, "run-detector-cases.sh");
        Directory.CreateDirectory(outputDirectoryPath);
        await File.WriteAllTextAsync(
            runnerScriptPath,
            CreateDetectorBatchScript(detectorScriptPath, outputDirectoryPath, detectorInputs));

        await TestProcessRunner.RunRequiredAsync(
            TestShellPaths.ResolveBashFileName(),
            [TestShellPaths.ToBashPath(runnerScriptPath)],
            repositoryPath,
            timeout: TimeSpan.FromSeconds(10));

        VerifyScopeDetectorCaseResult[] results = new VerifyScopeDetectorCaseResult[detectorInputs.Count];
        for (var i = 0; i < detectorInputs.Count; i++)
        {
            string outputPath = GetDetectorOutputPath(outputDirectoryPath, i);
            string output = await File.ReadAllTextAsync(outputPath);
            results[i] = new VerifyScopeDetectorCaseResult(
                detectorInputs[i].TestCase,
                ParseDetectorOutputs(output));
        }

        return results;
    }

    private static string CreateDetectorBatchScript (
        string detectorScriptPath,
        string outputDirectoryPath,
        IReadOnlyList<(VerifyScopeDetectorChangeCase TestCase, string BaseSha, string HeadSha)> detectorInputs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#!/usr/bin/env bash");
        builder.AppendLine("set -euo pipefail");
        builder.AppendLine("pids=()");
        for (var i = 0; i < detectorInputs.Count; i++)
        {
            var input = detectorInputs[i];
            string outputPath = TestShellPaths.ToBashPath(GetDetectorOutputPath(outputDirectoryPath, i));
            builder.AppendLine("(");
            builder.AppendLine("  export EVENT_NAME=pull_request");
            builder.Append("  export GITHUB_OUTPUT=").Append(TestShellPaths.QuoteBashArgument(outputPath)).AppendLine();
            builder.Append("  export PR_BASE_SHA=").Append(TestShellPaths.QuoteBashArgument(input.BaseSha)).AppendLine();
            builder.Append("  export PR_HEAD_SHA=").Append(TestShellPaths.QuoteBashArgument(input.HeadSha)).AppendLine();
            builder.AppendLine("  : > \"${GITHUB_OUTPUT}\"");
            builder.Append("  source ").Append(TestShellPaths.QuoteBashArgument(detectorScriptPath)).AppendLine(" > /dev/null");
            builder.AppendLine(") &");
            builder.AppendLine("pids+=(\"$!\")");
        }

        builder.AppendLine("for pid in \"${pids[@]}\"; do");
        builder.AppendLine("  wait \"${pid}\"");
        builder.AppendLine("done");
        return builder.ToString();
    }

    private static string GetDetectorOutputPath (
        string outputDirectoryPath,
        int index)
    {
        return Path.Combine(outputDirectoryPath, $"case-{index}.out");
    }

    private static void AssertDetectorOutput (
        IReadOnlyDictionary<string, string> outputs,
        string caseName,
        string outputName,
        string expectedValue)
    {
        Assert.True(
            outputs.TryGetValue(outputName, out string? actualValue),
            $"{caseName} did not emit {outputName}.");
        Assert.Equal(expectedValue, actualValue);
    }

    private static IReadOnlyDictionary<string, string> ParseDetectorOutputs (string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            values[line[..separatorIndex]] = line[(separatorIndex + 1)..];
        }

        return values;
    }

    private sealed record VerifyScopeDetectorChangeCase (
        string Name,
        string RelativePath,
        string InitialContents,
        string ChangedContents,
        string NeedsDotnet,
        string NeedsSharedPack,
        string NeedsCliPack,
        string NeedsUnity,
        string NeedsUnityPack,
        string NeedsReleasePack);

    private sealed record VerifyScopeDetectorCaseResult (
        VerifyScopeDetectorChangeCase TestCase,
        IReadOnlyDictionary<string, string> Outputs);
}
