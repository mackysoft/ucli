using System;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary>
    /// Binds one declared query condition to the typed result evaluator that establishes its verdict.
    /// </summary>
    /// <typeparam name="TResult"> The authoritative operation result DTO type. </typeparam>
    public sealed class UcliOperationVerdictDefinition<TResult>
    {
        private readonly Func<TResult, Verdict> evaluator;

        /// <summary> Initializes one typed operation verdict definition. </summary>
        /// <param name="description"> The condition that must hold for the result to produce <c>pass</c>. </param>
        /// <param name="evaluator"> The domain evaluator applied before the typed result is serialized. </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="description" /> is empty or whitespace.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="evaluator" /> is <see langword="null" />.
        /// </exception>
        public UcliOperationVerdictDefinition (
            string description,
            Func<TResult, Verdict> evaluator)
        {
            Contract = new UcliOperationVerdictContract(description);
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        internal UcliOperationVerdictContract Contract { get; }

        internal Verdict Evaluate (TResult result)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var verdict = evaluator(result);
            if (!Vocabulary.IsDefined(verdict))
            {
                throw new InvalidOperationException(
                    "The operation verdict evaluator returned an undefined contract value.");
            }

            return verdict;
        }
    }
}
