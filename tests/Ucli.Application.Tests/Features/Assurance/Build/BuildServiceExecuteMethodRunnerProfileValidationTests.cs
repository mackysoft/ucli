using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceExecuteMethodRunnerProfileValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMissingExecuteMethodEnvironment_ReturnsBuildRunnerEnvironmentMissingBeforeDispatch ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: """
                      "output": "${ucli.build.outputDir}"
                """,
            environment: """
                    "UCLI_SECRET"
                """);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            environmentVariableReader: new StubEnvironmentVariableReader(),
            requestExecutor: new UnexpectedUnityRequestExecutor(),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerEnvironmentMissing, error.Code);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("\"bad\": \"${ucli.build.unknown}\"")]
    [InlineData("\"bad\": \"${ucli.build.outputDir\"")]
    public async Task Execute_WithInvalidExecuteMethodArgumentVariable_ReturnsBuildProfileInvalidBeforeDispatch (
        string arguments)
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: arguments,
            environment: string.Empty);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: new UnexpectedUnityRequestExecutor(),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithEmptyExecuteMethodProfilePathVariable_ReturnsBuildProfileInvalidBeforeDispatch ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: """
                      "profile": "${ucli.build.profilePath}"
                """,
            environment: string.Empty);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(new BuildProfileFileReadResult(profileJson, string.Empty, null)),
            requestExecutor: new UnexpectedUnityRequestExecutor(),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithVariableLikeExecuteMethodEnvironmentName_DoesNotSubstituteBeforeLookup ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: """
                    "${UCLI_SECRET}"
                """);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            environmentVariableReader: new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["UCLI_SECRET"] = "secret-value",
            }),
            requestExecutor: new UnexpectedUnityRequestExecutor(),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerEnvironmentMissing, error.Code);
    }
}
