using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Enforces dangerous operation call authorization inside the Unity IPC server. </summary>
    internal sealed class DangerousOperationCallAuthorizer : IDangerousOperationCallAuthorizer
    {
        private readonly IPlanTokenEnvironment environment;

        /// <summary> Initializes a new instance of the <see cref="DangerousOperationCallAuthorizer" /> class. </summary>
        /// <param name="environment"> The runtime environment dependency used to locate project configuration. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="environment" /> is <see langword="null" />. </exception>
        public DangerousOperationCallAuthorizer (IPlanTokenEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <inheritdoc />
        public bool TryAuthorize (
            IReadOnlyList<PreparedOperation> preparedOperations,
            bool allowDangerous,
            out OperationFailure? failure)
        {
            if (preparedOperations == null)
            {
                throw new ArgumentNullException(nameof(preparedOperations));
            }

            PlanTokenConfigSnapshot? config = null;
            for (var i = 0; i < preparedOperations.Count; i++)
            {
                var preparedOperation = preparedOperations[i];
                var requiredPolicy = preparedOperation.PhaseOperation.Metadata.Policy;
                if (requiredPolicy != OperationPolicy.Dangerous)
                {
                    continue;
                }

                if (!allowDangerous)
                {
                    failure = CreateAllowDangerousFailure(preparedOperation.Operation);
                    return false;
                }

                config ??= PlanTokenConfigResolver.Resolve(environment.Capture().RepositoryRoot);
                if (!TryAuthorizeWithConfig(preparedOperation, requiredPolicy, config, out failure))
                {
                    return false;
                }
            }

            failure = null;
            return true;
        }

        internal static OperationFailure CreateAllowDangerousFailure (NormalizedOperation operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            return new OperationFailure(
                Code: OperationAuthorizationErrorCodes.OperationNotAllowed,
                Message: $"Step '{operation.Id}' requires dangerous operation '{operation.Op}'. Specify --allowDangerous to execute dangerous operations.",
                OpId: operation.Id);
        }

        private static bool TryAuthorizeWithConfig (
            PreparedOperation preparedOperation,
            OperationPolicy requiredPolicy,
            PlanTokenConfigSnapshot config,
            out OperationFailure? failure)
        {
            var operationName = preparedOperation.PhaseOperation.Metadata.OperationName;
            if (!VocabularyInputParser.TryParseIgnoreCase<OperationPolicy>(config.OperationPolicy, out var configuredPolicy)
                || requiredPolicy > configuredPolicy)
            {
                failure = CreateConfigFailure(
                    preparedOperation.Operation,
                    $"Operation '{operationName}' is blocked by operationPolicy='{config.OperationPolicy}'.");
                return false;
            }

            var allowlistResult = TryMatchAllowlist(operationName, config.OperationAllowlist);
            if (!allowlistResult.IsMatched)
            {
                failure = CreateConfigFailure(
                    preparedOperation.Operation,
                    allowlistResult.InvalidPattern != null
                        ? $"Operation allowlist contains invalid regex pattern: {allowlistResult.InvalidPattern}."
                        : $"Operation '{operationName}' does not match operationAllowlist.");
                return false;
            }

            failure = null;
            return true;
        }

        private static AllowlistMatchResult TryMatchAllowlist (
            string operationName,
            IReadOnlyList<string> allowlistPatterns)
        {
            if (allowlistPatterns == null || allowlistPatterns.Count == 0)
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

                if (!RegexPatternUtilities.TryIsMatch(operationName, pattern, out var isMatch))
                {
                    return AllowlistMatchResult.WithInvalidPattern(pattern);
                }

                if (isMatch)
                {
                    return AllowlistMatchResult.Matched();
                }
            }

            return AllowlistMatchResult.NotMatched();
        }

        private static OperationFailure CreateConfigFailure (
            NormalizedOperation operation,
            string message)
        {
            return new OperationFailure(
                Code: OperationAuthorizationErrorCodes.OperationNotAllowed,
                Message: message,
                OpId: operation.Id);
        }

        private readonly struct AllowlistMatchResult
        {
            public AllowlistMatchResult (
                bool isMatched,
                string? invalidPattern)
            {
                IsMatched = isMatched;
                InvalidPattern = invalidPattern;
            }

            public bool IsMatched { get; }

            public string? InvalidPattern { get; }

            public static AllowlistMatchResult Matched ()
            {
                return new AllowlistMatchResult(isMatched: true, invalidPattern: null);
            }

            public static AllowlistMatchResult NotMatched ()
            {
                return new AllowlistMatchResult(isMatched: false, invalidPattern: null);
            }

            public static AllowlistMatchResult WithInvalidPattern (string pattern)
            {
                return new AllowlistMatchResult(isMatched: false, invalidPattern: pattern);
            }
        }
    }
}
