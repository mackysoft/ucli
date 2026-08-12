using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.CsEval;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CsEvalCompilationServiceTests
    {
        [Test]
        [Category("Size.Small")]
        public void CompileAndValidate_WhenSnippetDeclaresType_ReturnsStructuredCompilationFailure ()
        {
            var service = CreateService();

            var result = service.CompileAndValidate(
                "public sealed class Prohibited { }",
                CsEvalSourceKind.Snippet,
                allowDangerous: true,
                allowPlayMode: false,
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Compile.Succeeded, Is.False);
            Assert.That(result.Compile.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Compile.Diagnostics[0].Id, Is.EqualTo(CsEvalDiagnosticIds.SnippetUnsupported));
            Assert.That(result.FailureMessage, Does.Contain("must not declare namespace, type, or member"));
        }

        [Test]
        [Category("Size.Small")]
        public void CompileAndValidate_WhenSnippetHasSyntaxError_ReturnsStructuredCompilationFailure ()
        {
            var service = CreateService();

            var result = service.CompileAndValidate(
                "context.DeclareNoChanges(\"unterminated);",
                CsEvalSourceKind.Snippet,
                allowDangerous: true,
                allowPlayMode: false,
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Compile.Succeeded, Is.False);
            Assert.That(result.Compile.Diagnostics, Is.Not.Empty);
            Assert.That(result.FailureMessage, Is.EqualTo("C# eval source failed to compile."));
        }

        [Test]
        [Category("Size.Small")]
        public void CompileAndValidate_WhenCompilationUnitHasNoEntryPoint_ReturnsStructuredCompilationFailure ()
        {
            var service = CreateService();

            var result = service.CompileAndValidate(
                "public sealed class NoEntryPoint { }",
                CsEvalSourceKind.CompilationUnit,
                allowDangerous: true,
                allowPlayMode: false,
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Compile.Succeeded, Is.False);
            Assert.That(result.Compile.Diagnostics, Is.Not.Empty);
            Assert.That(result.FailureMessage, Does.Contain("entry point"));
        }

        private static CsEvalCompilationService CreateService () => new(
            new CsEvalReferenceResolver(),
            new CsEvalEntryPointSymbolValidator(),
            new CsEvalSourcePreparer());
    }
}
