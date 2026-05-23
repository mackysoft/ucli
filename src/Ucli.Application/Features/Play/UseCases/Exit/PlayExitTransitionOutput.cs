using MackySoft.Ucli.Application.Features.Play.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Represents the public <c>play.exit</c> transition payload shape. </summary>
internal sealed record PlayExitTransitionOutput (
    string Transition,
    string Result,
    PlayLifecycleSnapshotOutput Before,
    PlayLifecycleSnapshotOutput? After,
    PlayLifecycleSnapshotOutput? Observed,
    string? ApplicationState);
