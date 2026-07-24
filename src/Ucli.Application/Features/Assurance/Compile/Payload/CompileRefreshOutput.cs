
namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents AssetDatabase refresh evidence grouped under <c>payload.compile.refresh</c>. </summary>
internal sealed record CompileRefreshOutput
{
    public CompileRefreshOutput (
        CompileRefreshOrigin Origin,
        bool Requested,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        bool Completed)
    {
        if (!TextVocabulary.IsDefined(Origin))
        {
            throw new ArgumentOutOfRangeException(nameof(Origin), Origin, "Unsupported compile refresh origin.");
        }

        this.Origin = Origin;
        this.Requested = Requested;
        this.StartedAtUtc = StartedAtUtc;
        this.CompletedAtUtc = CompletedAtUtc;
        this.Completed = Completed;
    }

    public CompileRefreshOrigin Origin { get; }

    public bool Requested { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; }

    public bool Completed { get; }
}
