namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the normalized Unity BuildReport artifact. </summary>
/// <param name="SchemaVersion"> The artifact schema version. </param>
/// <param name="Result"> The normalized BuildReport result literal. </param>
/// <param name="Target"> The Unity <c>BuildTarget</c> literal. </param>
/// <param name="OutputPath"> The BuildPipeline output path. </param>
/// <param name="DurationMilliseconds"> The BuildPipeline duration in milliseconds. </param>
/// <param name="TotalSizeBytes"> The BuildReport total output size in bytes. </param>
/// <param name="ErrorCount"> The normalized error count. </param>
/// <param name="WarningCount"> The normalized warning count. </param>
/// <param name="Steps"> The normalized BuildReport step summaries. </param>
/// <param name="Messages"> The normalized BuildReport messages. </param>
public sealed record IpcBuildReportArtifact (
    int SchemaVersion,
    string Result,
    string Target,
    string OutputPath,
    long DurationMilliseconds,
    long TotalSizeBytes,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<IpcBuildReportStep> Steps,
    IReadOnlyList<IpcBuildReportMessage> Messages);
