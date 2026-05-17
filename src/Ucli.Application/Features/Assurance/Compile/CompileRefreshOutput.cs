namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents AssetDatabase refresh evidence grouped under <c>payload.compile.refresh</c>. </summary>
internal sealed record CompileRefreshOutput (
    string Origin,
    bool Requested,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool Completed);
