using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Contracts;

/// <summary> Exposes the command fields consumed by <c>ops</c> preflight. </summary>
internal interface IOpsPreflightInputSource
{
    /// <summary> Gets the optional project path. </summary>
    string? ProjectPath { get; }

    /// <summary> Gets the normalized execution-mode value. </summary>
    UnityExecutionMode? Mode { get; }

    /// <summary> Gets the normalized timeout value in milliseconds. </summary>
    int? TimeoutMilliseconds { get; }

    /// <summary> Gets the optional normalized read-index mode override. </summary>
    ReadIndexMode? ReadIndexMode { get; }

    /// <summary> Gets a value indicating whether live source fallback should fail immediately. </summary>
    bool FailFast { get; }
}
