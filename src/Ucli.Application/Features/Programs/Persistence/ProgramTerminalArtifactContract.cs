namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Defines immutable artifacts owned by Program persistence. </summary>
internal static class ProgramTerminalArtifactContract
{
    public static ArtifactKind RunTerminalRecordKind { get; } = new("programRunTerminalRecord");
    public static ArtifactKind StepTerminalRecordKind { get; } = new("programStepTerminalRecord");
    public static ArtifactKind DefinitionSnapshotKind { get; } = new("programDefinitionSnapshot");
    public static ArtifactKind RequestPlanKind { get; } = new("programRequestPlan");
    public static ArtifactKind OperationDescriptorKind { get; } = new("programOperationDescriptor");
    public static ArtifactKind RequestResultKind { get; } = new("programRequestResult");
    public static ArtifactKind StepResultKind { get; } = new("programStepResult");
    public static ArtifactKind RejectedStepResultKind { get; } = new("programRejectedStepResult");
    public static ArtifactMediaType JsonMediaType { get; } = new("application/json");
}
