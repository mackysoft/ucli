using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal readonly record struct ProgressCommandServiceInvocation<TInput> (
    TInput Input,
    ICommandProgressSink? ProgressSink,
    CancellationToken CancellationToken);
