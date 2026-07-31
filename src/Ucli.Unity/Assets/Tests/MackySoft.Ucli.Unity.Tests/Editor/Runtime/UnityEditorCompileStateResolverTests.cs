using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorCompileStateResolverTests
    {
        [TestCase(false, false, UnityEditorCompileState.Ready)]
        [TestCase(true, false, UnityEditorCompileState.Compiling)]
        [TestCase(false, true, UnityEditorCompileState.Failed)]
        [TestCase(true, true, UnityEditorCompileState.Compiling)]
        [Category("Size.Small")]
        public void Resolve_WhenCompilationFlagsChange_ReturnsExpectedState (
            bool isCompiling,
            bool hasCompileFailure,
            UnityEditorCompileState expected)
        {
            var actual = UnityEditorCompileStateResolver.Resolve(isCompiling, hasCompileFailure);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
