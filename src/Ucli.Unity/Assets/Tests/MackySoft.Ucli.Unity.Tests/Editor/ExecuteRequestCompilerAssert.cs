using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Provides fluent assertions over one compiled public step and its lowered primitive operations. </summary>
    internal sealed class ExecuteRequestCompilerAssert
    {
        private readonly NormalizedRequestStep compiledStep;

        private readonly IReadOnlyList<NormalizedOperation> compiledOperations;

        /// <summary>
        /// Initializes a new assertion wrapper over one compiled public step and its lowered primitive operations.
        /// </summary>
        /// <param name="compiledStep"> The compiled public step. </param>
        /// <param name="compiledOperations"> The lowered primitive operations. </param>
        public ExecuteRequestCompilerAssert (
            NormalizedRequestStep compiledStep,
            IReadOnlyList<NormalizedOperation> compiledOperations)
        {
            this.compiledStep = compiledStep;
            this.compiledOperations = compiledOperations;
        }

        /// <summary>
        /// Asserts that the wrapped public step lowers into the expected primitive operation sequence.
        /// </summary>
        /// <param name="expectedKind"> The expected public step kind. </param>
        /// <param name="expectedOperationName"> The expected public operation name. </param>
        /// <param name="expectedPrimitiveOperationNames"> The expected primitive operation names in order. </param>
        /// <returns> The current assertion instance. </returns>
        public ExecuteRequestCompilerAssert HasLoweredOperations (
            IpcRequestStepKind expectedKind,
            string expectedOperationName,
            params string[] expectedPrimitiveOperationNames)
        {
            Assert.That(compiledStep.Kind, Is.EqualTo(expectedKind));
            Assert.That(compiledStep.OperationName, Is.EqualTo(expectedOperationName));
            Assert.That(compiledStep.PrimitiveCount, Is.EqualTo(expectedPrimitiveOperationNames.Length));
            return HasOperationNames(expectedPrimitiveOperationNames);
        }

        /// <summary>
        /// Asserts that lowered primitive operations match the expected operation names in order.
        /// </summary>
        /// <param name="expectedPrimitiveOperationNames"> The expected primitive operation names in order. </param>
        /// <returns> The current assertion instance. </returns>
        public ExecuteRequestCompilerAssert HasOperationNames (
            params string[] expectedPrimitiveOperationNames)
        {
            Assert.That(compiledOperations.Count, Is.EqualTo(expectedPrimitiveOperationNames.Length));
            for (var i = 0; i < expectedPrimitiveOperationNames.Length; i++)
            {
                Assert.That(compiledOperations[i].Op, Is.EqualTo(expectedPrimitiveOperationNames[i]));
            }

            return this;
        }

        /// <summary>
        /// Asserts that all lowered primitive operations share the same public step identifier.
        /// </summary>
        /// <param name="expectedId"> The expected public step identifier. </param>
        /// <returns> The current assertion instance. </returns>
        public ExecuteRequestCompilerAssert AllHavePublicId (string expectedId)
        {
            for (var i = 0; i < compiledOperations.Count; i++)
            {
                Assert.That(compiledOperations[i].Id, Is.EqualTo(expectedId));
            }

            return this;
        }

        /// <summary>
        /// Asserts that every lowered primitive operation exposes a non-empty internal execution key and that keys are unique.
        /// </summary>
        /// <returns> The current assertion instance. </returns>
        public ExecuteRequestCompilerAssert HaveDistinctInternalExecutionKeys ()
        {
            var uniqueKeys = new HashSet<string>();
            for (var i = 0; i < compiledOperations.Count; i++)
            {
                var executionKey = compiledOperations[i].InternalExecutionKey;
                Assert.That(executionKey, Is.Not.Null.And.Not.Empty);
                Assert.That(uniqueKeys.Add(executionKey!), Is.True);
            }

            return this;
        }

        /// <summary>
        /// Asserts that the selected operation target uses one project-asset selector and does not expose an asset selector.
        /// </summary>
        /// <param name="operationIndex"> The zero-based operation index. </param>
        /// <param name="expectedProjectAssetPath"> The expected project-asset path. </param>
        /// <returns> The current assertion instance. </returns>
        public ExecuteRequestCompilerAssert HasProjectAssetTarget (
            int operationIndex,
            string expectedProjectAssetPath)
        {
            var target = compiledOperations[operationIndex].Args.GetProperty("target");
            Assert.That(target.GetProperty("projectAssetPath").GetString(), Is.EqualTo(expectedProjectAssetPath));
            Assert.That(target.TryGetProperty("assetPath", out _), Is.False);
            return this;
        }
    }
}
