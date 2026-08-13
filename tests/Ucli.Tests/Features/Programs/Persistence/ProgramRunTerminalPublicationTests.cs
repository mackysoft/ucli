using MackySoft.Ucli.Application.Features.Programs.Persistence;

namespace MackySoft.Ucli.Tests.Features.Programs.Persistence;

public sealed class ProgramRunTerminalPublicationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TerminalArtifactContract_UsesProgramOwnedImmutableKinds ()
    {
        Assert.Equal("programRunTerminalRecord", ProgramTerminalArtifactContract.RunTerminalRecordKind.Value);
        Assert.Equal("programStepTerminalRecord", ProgramTerminalArtifactContract.StepTerminalRecordKind.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramStepTerminalArtifact_ExposesOnlyTheReferenceContractFields ()
    {
        var properties = typeof(ProgramStepTerminalArtifact)
            .GetProperties()
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([
            "ApplicationState", "ArtifactRefs", "ChildExecutionRef", "Command", "CompletedAtUtc", "DefinitionDigest",
            "ErrorCode", "GenerationAfter", "GenerationBefore", "LifecycleExecutionRef", "OperationDescriptorRefs",
            "RequestPlanRef", "RunId", "StartedAtUtc", "State", "StepResult", "Verdict",
        ], properties);
        Assert.DoesNotContain("SchemaVersion", properties);
        Assert.DoesNotContain("StepIndex", properties);
        Assert.DoesNotContain("StepResultRef", properties);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunTerminalArtifact_ExcludesStatusLocationAndSelfReference ()
    {
        var properties = typeof(ProgramRunTerminalArtifact)
            .GetProperties()
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([
            "ApplicationState", "Authorization", "Cancellation", "ChildExecutionRefs", "CompletedAtUtc", "Configuration",
            "CurrentEditorGeneration", "DeadlineUtc", "DefinitionDigest", "DefinitionSnapshotRef", "Project", "RunId",
            "SourceManifest", "StartedAtUtc", "State", "Steps", "Supervisor", "Terminal", "Verdict",
        ], properties);
        Assert.DoesNotContain("SchemaVersion", properties);
        Assert.DoesNotContain("TerminalRecordRef", properties);
        Assert.DoesNotContain("StatusLocator", properties);
    }
}
