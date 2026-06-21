namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one resolved runner output source candidate for artifact-store ingestion. </summary>
/// <param name="SourcePath"> The absolute source file or directory path before artifact-store ingestion. </param>
internal sealed record BuildOutputSourceEntry (
    string SourcePath);
