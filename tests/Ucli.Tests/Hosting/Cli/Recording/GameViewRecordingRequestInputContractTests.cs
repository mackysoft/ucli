using System.Text.Json;

namespace MackySoft.Ucli.Tests;

public sealed class GameViewRecordingRequestInputContractTests
{
    private const string ValidRequestJson =
        """
        {
          "schemaVersion": 1,
          "resolution": {
            "width": 320,
            "height": 240
          },
          "frameRate": 30
        }
        """;

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RecordingStart_WithRedirectedRequest_AcceptsInputBeforeCapabilityAdmission ()
    {
        using var scope = TestDirectories.CreateTempScope("recording-input", "standard-input");
        var unityProjectPath = await CreateProjectWithoutRecorderAsync(scope);

        var result = await CliProcessRunner.RunCommandWithStandardInputAsync(
            ValidRequestJson,
            UcliCommandNames.Recording,
            UcliCommandNames.StartSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var output = JsonAssert.ParseMultilineObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasSingleError(output.RootElement, GameViewRecordingErrorCodes.Unavailable);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RecordingStart_WithRequestPathAndRedirectedRequest_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("recording-input", "multiple-sources");
        var requestPath = await scope.WriteFileAsync("request.json", ValidRequestJson);

        var result = await CliProcessRunner.RunCommandWithStandardInputAsync(
            ValidRequestJson,
            UcliCommandNames.Recording,
            UcliCommandNames.StartSubcommand,
            "--requestPath",
            requestPath);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.RecordingStart);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[")]
    [InlineData("[]")]
    [Trait("Size", "Medium")]
    public async Task RecordingStart_WithInvalidRedirectedRequest_ReturnsInvalidArgument (string requestJson)
    {
        var result = await CliProcessRunner.RunCommandWithStandardInputAsync(
            requestJson,
            UcliCommandNames.Recording,
            UcliCommandNames.StartSubcommand);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.RecordingStart);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RecordingStart_WithOversizedRedirectedRequest_ReturnsInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommandWithStandardInputAsync(
            JsonSerializer.Serialize(new { value = new string('a', 64 * 1024) }),
            UcliCommandNames.Recording,
            UcliCommandNames.StartSubcommand);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.RecordingStart);
    }

    private static async Task<string> CreateProjectWithoutRecorderAsync (TestDirectoryScope scope)
    {
        var projectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await UnityProjectTestFactory.WriteUcliUnityPluginMarkerAsync(scope, "UnityProject");
        await scope.WriteFileAsync(
            Path.Combine("UnityProject", "Packages", "packages-lock.json"),
            """
            {
              "dependencies": {}
            }
            """);
        return projectPath;
    }
}
