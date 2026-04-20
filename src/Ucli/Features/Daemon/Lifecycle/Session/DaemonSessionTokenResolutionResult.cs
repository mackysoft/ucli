using System;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Session;

/// <summary> Represents the result of resolving one daemon session token value. </summary>
/// <param name="Token"> The resolved daemon session token when successful; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error when resolution fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionTokenResolutionResult (
    string? Token,
    ExecutionError? Error)
{
    /// <summary> Gets the message used when daemon session metadata is not present. </summary>
    public const string SessionNotAvailableMessage = "Daemon session is not available.";

    /// <summary> Gets a value indicating whether token resolution succeeded. </summary>
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Token) && Error is null;

    /// <summary> Gets a value indicating whether token resolution failed because session metadata is missing. </summary>
    public bool IsSessionNotAvailable =>
        Error is not null
        && Error.Kind == ExecutionErrorKind.InvalidArgument
        && string.Equals(Error.Message, SessionNotAvailableMessage, StringComparison.Ordinal);

    /// <summary> Creates a successful token-resolution result. </summary>
    /// <param name="token"> The resolved daemon session token. </param>
    /// <returns> The successful token-resolution result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="token" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static DaemonSessionTokenResolutionResult Success (string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new DaemonSessionTokenResolutionResult(token, null);
    }

    /// <summary> Creates a failed token-resolution result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed token-resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonSessionTokenResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonSessionTokenResolutionResult(null, error);
    }

    /// <summary> Creates a token-resolution result that indicates daemon session metadata is missing. </summary>
    /// <returns> The token-resolution result that indicates session metadata is unavailable. </returns>
    public static DaemonSessionTokenResolutionResult SessionNotAvailable ()
    {
        return Failure(ExecutionError.InvalidArgument(SessionNotAvailableMessage));
    }
}