using MackySoft.Ucli.Application.Features.Play.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Represents the public <c>play.enter</c> transition payload shape. </summary>
internal sealed record PlayEnterTransitionOutput (
    string Transition,
    string Result,
    PlayLifecycleSnapshotOutput Before,
    PlayLifecycleSnapshotOutput? After,
    PlayLifecycleSnapshotOutput? Observed,
    string? ApplicationState);
