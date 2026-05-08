using System.Collections;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.CsEval;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CsEvalOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public void Metadata_ExposesDangerousMutationAndCodeContract ()
        {
            var operation = new CsEvalOperation();

            Assert.That(operation.Metadata.OperationName, Is.EqualTo(UcliPrimitiveOperationNames.CsEval));
            Assert.That(operation.Metadata.Kind, Is.EqualTo(UcliOperationKind.Mutation));
            Assert.That(operation.Metadata.Policy, Is.EqualTo(OperationPolicy.Dangerous));
            Assert.That(operation.Metadata.RequiresPreCallPlanReplay, Is.True);
            Assert.That(operation.Metadata.DescribeContract.CodeContract, Is.Not.Null);
            Assert.That(operation.Metadata.DescribeContract.CodeContract!.EntryPoint!.Signature, Is.EqualTo("public static object? Run(UcliCsEvalContext context)"));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.ApiTypes!.Count, Is.EqualTo(1));
            var contextType = operation.Metadata.DescribeContract.CodeContract.ApiTypes![0];
            Assert.That(contextType.FullName, Is.EqualTo(typeof(UcliCsEvalContext).FullName));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.Log)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.DeclareTouchedAsset)));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSourceIsValid_DoesNotInvokeEntryPoint () => UniTask.ToCoroutine(async () =>
        {
            var operation = new CsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            throw new System.InvalidOperationException(""plan must not invoke entry point"");
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Plan(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.False);
            Assert.That(result.Changed, Is.False);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Succeeded));
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
            Assert.That(payload.TryGetProperty("logs", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSourceDoesNotCompile_ReturnsDiagnostics () => UniTask.ToCoroutine(async () =>
        {
            var operation = new CsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            return missingSymbol;
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Plan(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Failed));
            Assert.That(payload.GetProperty("compile").GetProperty("diagnostics").GetArrayLength(), Is.GreaterThan(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSourceReturnsJsonSerializableValue_ReturnsLogsAndTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var operation = new CsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.Log(""hello"");
            context.DeclareTouchedAsset(""Assets/Eval.asset"");
            return new { count = 3, label = ""ok"" };
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(result.Touched[0].Path, Is.EqualTo("Assets/Eval.asset"));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("durationMilliseconds").GetInt64(), Is.GreaterThanOrEqualTo(0));
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("hello"));
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Json));
            Assert.That(payload.GetProperty("returnValue").GetProperty("value").GetProperty("count").GetInt32(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnsTask_FailsBeforeInvocation () => UniTask.ToCoroutine(async () =>
        {
            var operation = new CsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static Task<object> Run(UcliCsEvalContext context)
        {
            return Task.FromResult<object>(new { value = 1 });
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Failed));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTouchedDeclarationConflicts_FailsWithLogs () => UniTask.ToCoroutine(async () =>
        {
            var operation = new CsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.Log(""before conflict"");
            context.DeclareNoTouchedResources();
            context.DeclareTouchedAsset(""Assets/Conflict.asset"");
            return new { done = true };
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("before conflict"));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        private static NormalizedOperation CreateOperation (
            string source,
            string entryPoint)
        {
            return new NormalizedOperation(
                Id: "cs-eval",
                Op: UcliPrimitiveOperationNames.CsEval,
                Args: JsonSerializer.SerializeToElement(new
                {
                    source,
                    entryPoint,
                }),
                As: null,
                Expect: null);
        }

        private static void AssertInvalidArgument (OperationPhaseStepResult result)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        }
    }
}
