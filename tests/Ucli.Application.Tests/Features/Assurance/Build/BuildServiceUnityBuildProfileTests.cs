using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceUnityBuildProfileTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnityBuildProfileInput_DelegatesInputResolutionToUnityAndProjectsResponse ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        const string unityBuildProfilePath = "Assets/BuildProfiles/Linux.asset";
        var unityBuildProfileDigest = new string('f', 64);
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "StandaloneLinux64",
                buildOptions: "None",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(unityBuildProfilePath, unityBuildProfileDigest));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        var output = result.Output!;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile), output.Build.Inputs.InputKind);
        Assert.Equal("standaloneLinux64", output.Build.Inputs.Target.StableName);
        Assert.Equal("StandaloneLinux64", output.Build.Inputs.Target.UnityBuildTarget);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile), output.Build.Inputs.Scenes.Source);
        Assert.Equal(["Assets/Scenes/ProfileMain.unity"], output.Build.Inputs.Scenes.Paths);
        Assert.False(output.Build.Inputs.Options.Development);
        var outputUnityBuildProfile = Assert.IsType<BuildUnityBuildProfileOutput>(output.Build.Inputs.UnityBuildProfile);
        Assert.Equal(unityBuildProfilePath, outputUnityBuildProfile.Path);
        Assert.Equal(unityBuildProfileDigest, outputUnityBuildProfile.Digest);

        BuildRunInvocationAssert.UnityBuildProfileInputDelegatedToUnity(
            requestExecutor,
            expectedUnityBuildProfilePath: unityBuildProfilePath);
        Assert.Null(artifactStore.PreparedOutputLayout);
        Assert.Equal("standaloneLinux64", artifactStore.AccountingRequest!.BuildTarget);

        Assert.NotNull(artifactStore.WrittenMetadata);
        var metadataInput = artifactStore.WrittenMetadata!.Inputs;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile), metadataInput.GetProperty("inputKind").GetString());
        var metadataUnityBuildProfile = metadataInput.GetProperty("unityBuildProfile");
        Assert.Equal(unityBuildProfilePath, metadataUnityBuildProfile.GetProperty("path").GetString());
        Assert.Equal(unityBuildProfileDigest, metadataUnityBuildProfile.GetProperty("digest").GetString());
        var metadataApplyAudit = metadataUnityBuildProfile.GetProperty("applyAudit");
        Assert.True(metadataApplyAudit.GetProperty("applied").GetBoolean());
        Assert.Equal("ready", metadataApplyAudit.GetProperty("lifecycleBefore").GetProperty("lifecycleState").GetString());
        Assert.Equal("asset-profile-after", metadataApplyAudit.GetProperty("generationsAfter").GetProperty("assetRefreshGeneration").GetString());
        Assert.False(metadataApplyAudit.GetProperty("dirtyStateAfter").GetProperty("dirty").GetBoolean());
        Assert.Equal(
            CreateExpectedPlayerLocationPathName(artifactStore.PreparedPaths!.RunnerOutputDirectory),
            artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("locationPathName").GetString());

        var validator = CreateBuildSemanticInvariantValidator();
        var semanticPayload = JsonSerializer.SerializeToElement(output, PayloadSerializerOptions);
        var semanticResult = validator.Validate(semanticPayload);
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnityBuildProfileAndroidAppBundleResponse_AcceptsResolvedAabOutputLayout ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        const string unityBuildProfilePath = "Assets/BuildProfiles/Linux.asset";
        var unityBuildProfileDigest = new string('f', 64);
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath, "Player.aab"));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "android",
                unityBuildTarget: "Android",
                buildOptions: "None",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(unityBuildProfilePath, unityBuildProfileDigest));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        Assert.Equal("android", artifactStore.AccountingRequest!.BuildTarget);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.Equal(
            CreateExpectedPlayerLocationPathName(artifactStore.PreparedPaths!.RunnerOutputDirectory, "Player.aab"),
            artifactStore.WrittenMetadata!.Runner.GetProperty("outputLayout").GetProperty("locationPathName").GetString());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithExplicitResponseContainingUnityBuildProfile_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                reportOutputPath: buildRunPayload.OutputLayout!.LocationPathName,
                outputLayout: buildRunPayload.OutputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset", new string('f', 64)));
        });
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnityBuildProfileResponseMismatchedUnityBuildTarget_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "Android",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset", new string('f', 64)));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnityBuildProfileResponseMissingApplyAuditDirtyState_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "StandaloneLinux64",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(
                    "Assets/BuildProfiles/Linux.asset",
                    new string('f', 64),
                    static audit => audit with
                    {
                        DirtyStateAfter = null!,
                    }));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnityBuildProfileResponseMismatchedApplyAuditGeneration_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "StandaloneLinux64",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(
                    "Assets/BuildProfiles/Linux.asset",
                    new string('f', 64),
                    static audit => audit with
                    {
                        GenerationsAfter = audit.GenerationsAfter with
                        {
                            CompileGeneration = "different-compile-generation",
                        },
                    }));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }
}
