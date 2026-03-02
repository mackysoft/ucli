using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides an in-memory operation registry implementation for phase execution. </summary>
    internal sealed class InMemoryPhaseOperationRegistry : IPhaseOperationRegistry
    {
        private readonly IReadOnlyDictionary<string, IPhaseOperation> operationsByName;

        /// <summary> Initializes a new instance of the <see cref="InMemoryPhaseOperationRegistry" /> class. </summary>
        /// <param name="operations"> The operation implementation collection. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operations" /> is <see langword="null" /> or contains a <see langword="null" /> operation. </exception>
        /// <exception cref="ArgumentException"> Thrown when operation name is invalid, contains leading or trailing whitespace, or is duplicated. </exception>
        public InMemoryPhaseOperationRegistry (IReadOnlyList<IPhaseOperation> operations)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            var dictionary = new Dictionary<string, IPhaseOperation>(StringComparer.Ordinal);
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i] ?? throw new ArgumentNullException(nameof(operations), "Operation list contains null.");
                var operationName = operation.OperationName;
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    throw new ArgumentException("Operation name must not be null, empty, or whitespace.", nameof(operations));
                }

                if (StringValueValidator.HasOuterWhitespace(operationName))
                {
                    throw new ArgumentException($"Operation name must not contain leading or trailing whitespace: '{operationName}'.", nameof(operations));
                }

                if (dictionary.ContainsKey(operationName))
                {
                    throw new ArgumentException($"Operation name is duplicated: '{operationName}'.", nameof(operations));
                }

                dictionary.Add(operationName, operation);
            }

            operationsByName = dictionary;
        }

        /// <summary> Attempts to resolve an operation implementation by operation name. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="operation"> The resolved implementation when found. </param>
        /// <returns> <see langword="true" /> when operation implementation was found; otherwise <see langword="false" />. </returns>
        public bool TryResolve (
            string operationName,
            out IPhaseOperation operation)
        {
            return operationsByName.TryGetValue(operationName, out operation);
        }
    }
}
