namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents resolved build options persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunOptionsMetadata (
    bool Development);
