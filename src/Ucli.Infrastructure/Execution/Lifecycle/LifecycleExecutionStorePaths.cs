using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Owns the private project-local layout used to reconnect one Lifecycle Execution.
/// </summary>
internal sealed class LifecycleExecutionStorePaths
{
    private const string ExecutionsDirectoryName = "lifecycle-executions";
    private const string RecordFileName = "execution.json";
    private const string LockFileName = "execution.lock";
    private const string TerminalRecordsDirectoryName = "lifecycle-execution";
    private const string TerminalRecordFileName = "terminal.json";

    private readonly AbsolutePath storageRoot;
    private readonly ProjectFingerprint projectFingerprint;

    public LifecycleExecutionStorePaths (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        this.storageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
        this.projectFingerprint = projectFingerprint
            ?? throw new ArgumentNullException(nameof(projectFingerprint));
    }

    public AbsolutePath StorageRoot => storageRoot;

    public AbsolutePath ExecutionsDirectory => ResolveUnderProject(ExecutionsDirectoryName);

    public AbsolutePath ResolveKindDirectory (LifecycleExecutionKind kind)
    {
        return ResolveUnderProject(
            ExecutionsDirectoryName,
            GetKindSegment(kind));
    }

    public AbsolutePath ResolveExecutionDirectory (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return ResolveUnderProject(
            ExecutionsDirectoryName,
            GetKindSegment(kind),
            StoragePathSegmentCodec.EncodeGuid(executionId, nameof(executionId)));
    }

    public AbsolutePath ResolveRecordPath (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return ResolveUnderExecution(kind, executionId, RecordFileName);
    }

    public AbsolutePath ResolveLockPath (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return ResolveUnderExecution(kind, executionId, LockFileName);
    }

    public AbsolutePath ResolveCheckpointPath (
        LifecycleExecutionKind kind,
        Guid executionId,
        string checkpointFileName)
    {
        if (string.IsNullOrWhiteSpace(checkpointFileName))
        {
            throw new ArgumentException(
                "Checkpoint file name must not be empty.",
                nameof(checkpointFileName));
        }

        return ResolveUnderExecution(kind, executionId, checkpointFileName);
    }

    public ContainedPath ResolveTerminalRecordPath (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return ContainedPath.Create(
            storageRoot,
            RootRelativePath.Parse(GetTerminalRecordRelativePath(kind, executionId)));
    }

    public ArtifactPath CreateTerminalRecordArtifactPath (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return new ArtifactPath(GetTerminalRecordRelativePath(kind, executionId));
    }

    public bool HasExpectedTerminalRecordArtifactPath (
        LifecycleExecutionKind kind,
        Guid executionId,
        ArtifactRef artifactReference)
    {
        if (artifactReference is null)
        {
            throw new ArgumentNullException(nameof(artifactReference));
        }

        var actualPath = artifactReference switch
        {
            PathArtifactRef pathArtifact => pathArtifact.Path,
            PathAndUriArtifactRef pathAndUriArtifact => pathAndUriArtifact.Path,
            _ => null,
        };
        return actualPath == CreateTerminalRecordArtifactPath(kind, executionId);
    }

    public ExecutionStatusLocator CreateStatusLocator (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return new ExecutionStatusLocator(GetRecordRelativePath(kind, executionId));
    }

    public bool HasExpectedStatusLocator (
        LifecycleExecutionKind kind,
        Guid executionId,
        ExecutionStatusLocator? statusLocator)
    {
        return statusLocator is not null
            && string.Equals(
                statusLocator.Value,
                GetRecordRelativePath(kind, executionId),
                StringComparison.Ordinal);
    }

    private AbsolutePath ResolveUnderProject (params string[] relativeSegments)
    {
        var projectDirectory = UcliStoragePathResolver.ResolveProjectDirectory(
            storageRoot,
            projectFingerprint);
        return ResolveUnder(projectDirectory, relativeSegments);
    }

    private AbsolutePath ResolveUnderExecution (
        LifecycleExecutionKind kind,
        Guid executionId,
        string fileName)
    {
        return ResolveUnder(
            ResolveExecutionDirectory(kind, executionId),
            fileName);
    }

    private static AbsolutePath ResolveUnder (
        AbsolutePath boundaryRoot,
        params string[] relativeSegments)
    {
        return ContainedPath.Create(
            boundaryRoot,
            RootRelativePath.Parse(string.Join("/", relativeSegments))).Target;
    }

    private string GetRecordRelativePath (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return string.Join(
            "/",
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.ProjectsDirectoryName,
            StoragePathSegmentCodec.EncodeProjectFingerprint(projectFingerprint),
            ExecutionsDirectoryName,
            GetKindSegment(kind),
            StoragePathSegmentCodec.EncodeGuid(executionId, nameof(executionId)),
            RecordFileName);
    }

    private string GetTerminalRecordRelativePath (
        LifecycleExecutionKind kind,
        Guid executionId)
    {
        return string.Join(
            "/",
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.ProjectsDirectoryName,
            StoragePathSegmentCodec.EncodeProjectFingerprint(projectFingerprint),
            UcliStoragePathNames.ArtifactsDirectoryName,
            TerminalRecordsDirectoryName,
            GetKindSegment(kind),
            StoragePathSegmentCodec.EncodeGuid(executionId, nameof(executionId)),
            TerminalRecordFileName);
    }

    private static string GetKindSegment (LifecycleExecutionKind kind)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Lifecycle Execution kind must be defined.");
        }

        return TextVocabulary.GetText(kind);
    }
}
