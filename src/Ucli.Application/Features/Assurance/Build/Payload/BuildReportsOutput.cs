using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Holds the finite report artifacts emitted by one build run. </summary>
internal sealed record BuildReportsOutput
{
    public BuildReportsOutput (
        AssuranceReportReference Build,
        AssuranceReportReference? BuildReport,
        AssuranceReportReference BuildOutputManifest,
        AssuranceReportReference BuildLog)
    {
        this.Build = Build ?? throw new ArgumentNullException(nameof(Build));
        this.BuildReport = BuildReport;
        this.BuildOutputManifest = BuildOutputManifest
            ?? throw new ArgumentNullException(nameof(BuildOutputManifest));
        this.BuildLog = BuildLog ?? throw new ArgumentNullException(nameof(BuildLog));
    }

    public AssuranceReportReference Build { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssuranceReportReference? BuildReport { get; }

    public AssuranceReportReference BuildOutputManifest { get; }

    public AssuranceReportReference BuildLog { get; }
}
