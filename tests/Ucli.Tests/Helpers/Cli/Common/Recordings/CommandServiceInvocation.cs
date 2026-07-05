namespace MackySoft.Tests;

internal readonly record struct CommandServiceInvocation<TInput> (
    TInput Input,
    CancellationToken CancellationToken);
