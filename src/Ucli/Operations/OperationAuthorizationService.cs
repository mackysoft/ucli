using System.Text.RegularExpressions;
using MackySoft.Ucli.Configuration;

namespace MackySoft.Ucli.Operations;

/// <summary> Evaluates operation authorization using operationPolicy and operationAllowlist constraints. </summary>
internal sealed class OperationAuthorizationService : IOperationAuthorizationService
{
    /// <summary> Asynchronously evaluates operation policy and allowlist constraints for one operation. </summary>
    /// <param name="operation"> The operation descriptor to evaluate. </param>
    /// <param name="config"> The configuration values that define execution constraints. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the authorization evaluation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operation" /> or <paramref name="config" /> is <see langword="null" />. </exception>
    public ValueTask<OperationAuthorizationResult> Authorize (
        UcliOperationDescriptor operation,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(config);

        if (!IsPolicyAllowed(operation.Policy, config.OperationPolicy))
        {
            return ValueTask.FromResult(OperationAuthorizationResult.Denied(
                ValidationErrorCodes.OperationNotAllowed,
                $"Operation '{operation.Name}' is blocked by operationPolicy='{config.OperationPolicy}'."));
        }

        var allowlistResult = TryMatchAllowlist(operation.Name, config.OperationAllowlist);
        if (!allowlistResult.IsMatched)
        {
            if (allowlistResult.InvalidPattern is not null)
            {
                return ValueTask.FromResult(OperationAuthorizationResult.Denied(
                    ValidationErrorCodes.OperationNotAllowed,
                    $"Operation allowlist contains invalid regex pattern: {allowlistResult.InvalidPattern}."));
            }

            return ValueTask.FromResult(OperationAuthorizationResult.Denied(
                ValidationErrorCodes.OperationNotAllowed,
                $"Operation '{operation.Name}' does not match operationAllowlist."));
        }

        return ValueTask.FromResult(OperationAuthorizationResult.Allowed());
    }

    /// <summary> Determines whether required operation policy is within configured allowance. </summary>
    /// <param name="requiredPolicy"> The required policy for one operation. </param>
    /// <param name="configuredPolicy"> The configured upper bound policy. </param>
    /// <returns> <see langword="true" /> when operation is allowed by policy level; otherwise <see langword="false" />. </returns>
    private static bool IsPolicyAllowed (
        OperationPolicy requiredPolicy,
        OperationPolicy configuredPolicy)
    {
        return requiredPolicy <= configuredPolicy;
    }

    /// <summary> Attempts to match operation name against allowlist patterns. </summary>
    /// <param name="operationName"> The operation name. </param>
    /// <param name="allowlistPatterns"> The configured allowlist patterns. </param>
    /// <returns> The allowlist match result. </returns>
    private static AllowlistMatchResult TryMatchAllowlist (
        string operationName,
        IReadOnlyList<string> allowlistPatterns)
    {
        if (allowlistPatterns is null || allowlistPatterns.Count == 0)
        {
            return AllowlistMatchResult.NotMatched();
        }

        for (var i = 0; i < allowlistPatterns.Count; i++)
        {
            var pattern = allowlistPatterns[i];
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            try
            {
                if (Regex.IsMatch(operationName, pattern, RegexOptions.CultureInvariant))
                {
                    return AllowlistMatchResult.Matched();
                }
            }
            catch (ArgumentException)
            {
                return AllowlistMatchResult.WithInvalidPattern(pattern);
            }
        }

        return AllowlistMatchResult.NotMatched();
    }

    /// <summary> Represents the result of allowlist regex matching. </summary>
    /// <param name="IsMatched"> Whether any allowlist pattern matched. </param>
    /// <param name="InvalidPattern"> The invalid pattern when regex parsing failed. </param>
    private readonly record struct AllowlistMatchResult (
        bool IsMatched,
        string? InvalidPattern)
    {
        /// <summary> Creates a matched result. </summary>
        /// <returns> A matched result. </returns>
        public static AllowlistMatchResult Matched ()
        {
            return new AllowlistMatchResult(IsMatched: true, InvalidPattern: null);
        }

        /// <summary> Creates a not-matched result. </summary>
        /// <returns> A not-matched result. </returns>
        public static AllowlistMatchResult NotMatched ()
        {
            return new AllowlistMatchResult(IsMatched: false, InvalidPattern: null);
        }

        /// <summary> Creates an invalid-pattern result. </summary>
        /// <param name="pattern"> The invalid regex pattern. </param>
        /// <returns> An invalid-pattern result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pattern" /> is <see langword="null" />. </exception>
        public static AllowlistMatchResult WithInvalidPattern (string pattern)
        {
            ArgumentNullException.ThrowIfNull(pattern);
            return new AllowlistMatchResult(IsMatched: false, InvalidPattern: pattern);
        }
    }
}
