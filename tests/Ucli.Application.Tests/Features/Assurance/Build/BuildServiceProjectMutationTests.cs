using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceProjectMutationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithForbidProjectMutation_ReturnsCommandFailureAfterWritingMetadata ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: true,
                    mode: BuildProfileProjectMutationMode.Forbid)),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProjectMutationForbidden, error.Code);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.True(artifactStore.WrittenMetadata!.ProjectMutation.GetProperty("mutated").GetBoolean());
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithForbidProjectMutationPartialCoverage_ReturnsCommandFailureAfterWritingMetadata ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: false,
                    mode: BuildProfileProjectMutationMode.Forbid,
                    coverage: IpcBuildProjectMutationAuditCoverage.Partial)),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProjectMutationForbidden, error.Code);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.False(artifactStore.WrittenMetadata!.ProjectMutation.GetProperty("mutated").GetBoolean());
        Assert.Equal("partial", artifactStore.WrittenMetadata.ProjectMutation.GetProperty("coverage").GetString());
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithAuditProjectMutation_ReturnsNonBlockingResidualRisk ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["daemon", "oneshot"], ["batchmode", "gui"], "audit"),
                "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: true,
                    mode: BuildProfileProjectMutationMode.Audit)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(AssuranceVerdict.Pass, result.Output!.Verdict);
        var risk = Assert.Single(result.Output.ResidualRisks);
        Assert.Equal(BuildRiskCodes.ProjectMutationDetected.Value, risk.Code);
        Assert.False(risk.Blocking);
        Assert.Equal(AssuranceClaimStatus.Passed, FindClaim(result.Output, BuildClaimCodes.UnityBuildProjectMutationAccounted).Status);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithAllowWithAuditFullCoverage_ReturnsSuccessWithoutResidualRisk ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["daemon", "oneshot"], ["batchmode", "gui"], "allowWithAudit"),
                "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: true,
                    mode: BuildProfileProjectMutationMode.AllowWithAudit)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(AssuranceVerdict.Pass, result.Output!.Verdict);
        Assert.Empty(result.Output.ResidualRisks);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithAllowWithAuditPartialCoverage_ReturnsNonBlockingResidualRisk ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["daemon", "oneshot"], ["batchmode", "gui"], "allowWithAudit"),
                "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: false,
                    mode: BuildProfileProjectMutationMode.AllowWithAudit,
                    coverage: IpcBuildProjectMutationAuditCoverage.Partial)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(AssuranceVerdict.Incomplete, result.Output!.Verdict);
        var risk = Assert.Single(result.Output.ResidualRisks);
        Assert.Equal(BuildRiskCodes.ProjectMutationDetected.Value, risk.Code);
        Assert.False(risk.Blocking);
        Assert.Equal(AssuranceClaimStatus.Indeterminate, FindClaim(result.Output, BuildClaimCodes.UnityBuildProjectMutationAccounted).Status);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMismatchedProjectMutationModeResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: false,
                    mode: BuildProfileProjectMutationMode.Audit)),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

}
