namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the public projection of a build runner terminal result. </summary>
internal sealed record BuildRunnerResultOutput (
    string Source,
    string Status);
