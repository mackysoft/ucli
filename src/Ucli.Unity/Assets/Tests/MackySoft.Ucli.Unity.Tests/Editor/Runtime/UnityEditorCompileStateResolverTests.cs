using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorCompileStateResolverTests
    {
        [TestCase(false, false, IpcCompileState.Ready)]
        [TestCase(true, false, IpcCompileState.Compiling)]
        [TestCase(false, true, IpcCompileState.Failed)]
        [TestCase(true, true, IpcCompileState.Compiling)]
        [Category("Size.Small")]
        public void Resolve_WhenCompilationFlagsChange_ReturnsExpectedState (
            bool isCompiling,
            bool hasCompileFailure,
            IpcCompileState expected)
        {
            var actual = UnityEditorCompileStateResolver.Resolve(isCompiling, hasCompileFailure);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
