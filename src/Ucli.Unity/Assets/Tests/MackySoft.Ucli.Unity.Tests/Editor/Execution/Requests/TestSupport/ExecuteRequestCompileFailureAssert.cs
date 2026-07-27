using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Provides fluent assertions over one compile-time normalization failure. </summary>
    internal sealed class ExecuteRequestCompileFailureAssert
    {
        private readonly ExecuteRequestNormalizationError error;

        /// <summary>
        /// Initializes a new assertion wrapper over one compile-time normalization failure.
        /// </summary>
        /// <param name="error"> The compile-time normalization failure. </param>
        public ExecuteRequestCompileFailureAssert (ExecuteRequestNormalizationError error)
        {
            this.error = error;
        }

        /// <summary>
        /// Asserts that the wrapped failure is one invalid-argument error for the expected request value.
        /// </summary>
        /// <param name="expectedInstancePath"> The expected RFC 6901 instance path. </param>
        /// <returns> The current assertion instance. </returns>
        public ExecuteRequestCompileFailureAssert HasInvalidArgument (string expectedInstancePath)
        {
            Assert.That(error.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(error.InstancePath, Is.EqualTo(expectedInstancePath));
            return this;
        }

        /// <summary>
        /// Asserts that the wrapped failure message contains the expected fragment.
        /// </summary>
        /// <param name="expectedMessageFragment"> The expected failure-message fragment. </param>
        /// <returns> The current assertion instance. </returns>
        public ExecuteRequestCompileFailureAssert HasMessageContaining (string expectedMessageFragment)
        {
            Assert.That(error.Message, Does.Contain(expectedMessageFragment));
            return this;
        }
    }
}
