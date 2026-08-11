namespace MackySoft.Ucli.Application.Shared.Execution.Process;

/// <summary>Defines the observable states of one exact operating-system process generation.</summary>
internal enum ProcessIdentityStatus
{
    /// <summary>The expected process generation is still running.</summary>
    Matching = 1,

    /// <summary>The expected process exited, or its identifier belongs to another generation.</summary>
    ExitedOrReplaced,

    /// <summary>The current environment did not provide enough evidence to decide.</summary>
    Unobservable,
}
