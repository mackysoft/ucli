using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Represents the result of resolving one daemon session token value. </summary>
/// <param name="Token"> The resolved daemon session token when successful; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error when resolution fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionTokenResolutionResult (
    string? Token,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether token resolution succeeded. </summary>
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Token) && Error is null;

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
}