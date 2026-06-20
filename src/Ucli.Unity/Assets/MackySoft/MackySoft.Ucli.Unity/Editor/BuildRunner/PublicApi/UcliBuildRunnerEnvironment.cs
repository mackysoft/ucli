using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents process environment entries resolved for a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerEnvironment
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerEnvironment" /> class. </summary>
        internal UcliBuildRunnerEnvironment (
            IReadOnlyDictionary<string, string> variables,
            IReadOnlyDictionary<string, string> secrets)
        {
            Variables = variables ?? throw new ArgumentNullException(nameof(variables));
            Secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        }

        /// <summary> Gets non-secret process environment entries resolved by uCLI for this runner invocation. </summary>
        public IReadOnlyDictionary<string, string> Variables { get; }

        /// <summary> Gets secret process environment entries resolved by uCLI for this runner invocation. </summary>
        public IReadOnlyDictionary<string, string> Secrets { get; }
    }
}
