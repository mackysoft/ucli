using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one established evidence entry in a build assurance claim. </summary>
internal abstract record BuildEvidenceOutput
{
    protected BuildEvidenceOutput (BuildEvidenceKind kind)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported build evidence kind.");
        }

        Kind = kind;
    }

    [JsonIgnore]
    public BuildEvidenceKind Kind { get; }
}

/// <summary> Represents build evidence carried directly in the claim. </summary>
internal abstract record BuildInlineEvidenceOutput<TData> : BuildEvidenceOutput
    where TData : class
{
    protected BuildInlineEvidenceOutput (
        BuildEvidenceKind kind,
        TData data)
        : base(kind)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public TData Data { get; }
}

/// <summary> Represents build evidence linked to a persisted build report. </summary>
internal abstract record BuildReferencedInlineEvidenceOutput : BuildEvidenceOutput
{
    protected BuildReferencedInlineEvidenceOutput (
        BuildEvidenceKind kind,
        BuildArtifactKind evidenceRef)
        : base(kind)
    {
        if (!TextVocabulary.IsDefined(evidenceRef))
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceRef), evidenceRef, "Unsupported build evidence reference.");
        }

        EvidenceRef = evidenceRef;
    }

    public BuildArtifactKind EvidenceRef { get; }
}

/// <summary> Represents typed build evidence linked to a persisted build report. </summary>
internal abstract record BuildReferencedInlineEvidenceOutput<TData> : BuildReferencedInlineEvidenceOutput
    where TData : class
{
    protected BuildReferencedInlineEvidenceOutput (
        BuildEvidenceKind kind,
        BuildArtifactKind evidenceRef,
        TData data)
        : base(kind, evidenceRef)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public TData Data { get; }
}

internal sealed record BuildProfileEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildProfileOutput>
{
    private BuildProfileEvidenceOutput (BuildProfileOutput data)
        : base(BuildEvidenceKind.BuildProfile, BuildArtifactKind.Build, data)
    {
    }

    public static BuildProfileEvidenceOutput Create (BuildProfileOutput data)
    {
        return new BuildProfileEvidenceOutput(data);
    }
}

internal sealed record BuildInputEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<IpcBuildInputProbe>
{
    private BuildInputEvidenceOutput (IpcBuildInputProbe data)
        : base(BuildEvidenceKind.BuildInput, BuildArtifactKind.Build, data)
    {
    }

    public static BuildInputEvidenceOutput Create (IpcBuildInputProbe data)
    {
        return new BuildInputEvidenceOutput(data);
    }
}

internal sealed record BuildLifecycleEvidenceOutput
    : BuildInlineEvidenceOutput<IpcUnityEditorObservation>
{
    private BuildLifecycleEvidenceOutput (IpcUnityEditorObservation data)
        : base(BuildEvidenceKind.ReadyLifecycleSnapshot, data)
    {
    }

    public static BuildLifecycleEvidenceOutput Create (IpcUnityEditorObservation data)
    {
        return new BuildLifecycleEvidenceOutput(data);
    }
}

internal sealed record BuildRunnerEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildRunnerOutput>
{
    private BuildRunnerEvidenceOutput (BuildRunnerOutput data)
        : base(BuildEvidenceKind.BuildRunner, BuildArtifactKind.Build, data)
    {
    }

    public static BuildRunnerEvidenceOutput Create (BuildRunnerOutput data)
    {
        return new BuildRunnerEvidenceOutput(data);
    }
}

internal sealed record BuildReportSummaryEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildSummaryOutput>
{
    private BuildReportSummaryEvidenceOutput (BuildSummaryOutput data)
        : base(BuildEvidenceKind.BuildReportSummary, BuildArtifactKind.BuildReport, data)
    {
    }

    public static BuildReportSummaryEvidenceOutput Create (BuildSummaryOutput data)
    {
        return new BuildReportSummaryEvidenceOutput(data);
    }
}

internal sealed record BuildSummaryEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildSummaryOutput>
{
    private BuildSummaryEvidenceOutput (BuildSummaryOutput data)
        : base(BuildEvidenceKind.BuildSummary, BuildArtifactKind.Build, data)
    {
    }

    public static BuildSummaryEvidenceOutput Create (BuildSummaryOutput data)
    {
        return new BuildSummaryEvidenceOutput(data);
    }
}

internal sealed record BuildRunnerResultEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildRunnerResultOutput>
{
    private BuildRunnerResultEvidenceOutput (BuildRunnerResultOutput data)
        : base(BuildEvidenceKind.BuildRunnerResult, BuildArtifactKind.Build, data)
    {
    }

    public static BuildRunnerResultEvidenceOutput Create (BuildRunnerResultOutput data)
    {
        return new BuildRunnerResultEvidenceOutput(data);
    }
}

internal sealed record BuildLogEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildLogsOutput>
{
    private BuildLogEvidenceOutput (BuildLogsOutput data)
        : base(BuildEvidenceKind.BuildLogSummary, BuildArtifactKind.BuildLog, data)
    {
    }

    public static BuildLogEvidenceOutput Create (BuildLogsOutput data)
    {
        return new BuildLogEvidenceOutput(data);
    }
}

internal sealed record BuildOutputAccountingEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildArtifactOutput>
{
    private BuildOutputAccountingEvidenceOutput (BuildArtifactOutput data)
        : base(BuildEvidenceKind.BuildOutputAccounting, BuildArtifactKind.Build, data)
    {
    }

    public static BuildOutputAccountingEvidenceOutput Create (BuildArtifactOutput data)
    {
        return new BuildOutputAccountingEvidenceOutput(data);
    }
}

internal sealed record BuildOutputManifestEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildArtifactOutput>
{
    private BuildOutputManifestEvidenceOutput (BuildArtifactOutput data)
        : base(BuildEvidenceKind.BuildOutputManifest, BuildArtifactKind.BuildOutputManifest, data)
    {
    }

    public static BuildOutputManifestEvidenceOutput Create (BuildArtifactOutput data)
    {
        return new BuildOutputManifestEvidenceOutput(data);
    }
}

internal sealed record BuildGenerationEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<BuildGenerationsOutput>
{
    private BuildGenerationEvidenceOutput (BuildGenerationsOutput data)
        : base(BuildEvidenceKind.GenerationSnapshot, BuildArtifactKind.Build, data)
    {
    }

    public static BuildGenerationEvidenceOutput Create (BuildGenerationsOutput data)
    {
        return new BuildGenerationEvidenceOutput(data);
    }
}

internal sealed record BuildProjectMutationEvidenceOutput
    : BuildReferencedInlineEvidenceOutput<IpcBuildProjectMutationAudit>
{
    private BuildProjectMutationEvidenceOutput (IpcBuildProjectMutationAudit data)
        : base(BuildEvidenceKind.ProjectMutationAudit, BuildArtifactKind.Build, data)
    {
    }

    public static BuildProjectMutationEvidenceOutput Create (IpcBuildProjectMutationAudit data)
    {
        return new BuildProjectMutationEvidenceOutput(data);
    }
}
