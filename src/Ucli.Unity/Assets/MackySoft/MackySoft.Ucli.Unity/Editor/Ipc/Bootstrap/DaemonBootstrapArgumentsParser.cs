using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements daemon bootstrap command-line argument parsing. </summary>
    internal sealed class DaemonBootstrapArgumentsParser : IDaemonBootstrapArgumentsParser
    {
        /// <summary> Parses daemon bootstrap command-line arguments. </summary>
        /// <param name="args"> The process command-line arguments. </param>
        /// <param name="bootstrapArguments"> The parsed bootstrap argument model. </param>
        /// <param name="errorMessage"> The error message when parse fails. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public bool TryParse (
            string[] args,
            out DaemonBootstrapArguments bootstrapArguments,
            out string errorMessage)
        {
            bootstrapArguments = default;
            errorMessage = string.Empty;

            if (args == null)
            {
                errorMessage = "uCLI daemon bootstrap arguments are missing.";
                return false;
            }

            if (!TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.RepositoryRoot, out var repositoryRoot)
                || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.ProjectFingerprint, out var projectFingerprint)
                || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.SessionPath, out var sessionPath)
                || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.EndpointTransportKind, out var endpointTransportKind)
                || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.EndpointAddress, out var endpointAddress))
            {
                errorMessage = "uCLI daemon bootstrap arguments are missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(repositoryRoot)
                || string.IsNullOrWhiteSpace(projectFingerprint)
                || string.IsNullOrWhiteSpace(sessionPath)
                || string.IsNullOrWhiteSpace(endpointTransportKind)
                || string.IsNullOrWhiteSpace(endpointAddress))
            {
                errorMessage = "uCLI daemon bootstrap arguments must not be empty.";
                return false;
            }

            bootstrapArguments = new DaemonBootstrapArguments(
                RepositoryRoot: repositoryRoot,
                ProjectFingerprint: projectFingerprint,
                SessionPath: sessionPath,
                EndpointTransportKind: endpointTransportKind,
                EndpointAddress: endpointAddress);
            return true;
        }

        /// <summary> Tries to extract one command-line argument value. </summary>
        /// <param name="args"> The command-line argument array. </param>
        /// <param name="argumentName"> The target argument name. </param>
        /// <param name="value"> The resolved argument value. </param>
        /// <returns> <see langword="true" /> when argument value exists; otherwise <see langword="false" />. </returns>
        private static bool TryGetArgumentValue (
            string[] args,
            string argumentName,
            out string value)
        {
            value = string.Empty;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], argumentName, StringComparison.Ordinal))
                {
                    continue;
                }

                var nextToken = args[i + 1];
                if (IsKnownArgumentName(nextToken))
                {
                    continue;
                }

                value = nextToken;
                return true;
            }

            return false;
        }

        /// <summary> Determines whether one token is a known daemon bootstrap argument name. </summary>
        /// <param name="token"> The command-line token. </param>
        /// <returns> <see langword="true" /> when token is a known argument name; otherwise <see langword="false" />. </returns>
        private static bool IsKnownArgumentName (string token)
        {
            return string.Equals(token, IpcDaemonBootstrapArgumentNames.RepositoryRoot, StringComparison.Ordinal)
                || string.Equals(token, IpcDaemonBootstrapArgumentNames.ProjectFingerprint, StringComparison.Ordinal)
                || string.Equals(token, IpcDaemonBootstrapArgumentNames.SessionPath, StringComparison.Ordinal)
                || string.Equals(token, IpcDaemonBootstrapArgumentNames.EndpointTransportKind, StringComparison.Ordinal)
                || string.Equals(token, IpcDaemonBootstrapArgumentNames.EndpointAddress, StringComparison.Ordinal);
        }
    }
}
