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
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CsEvalOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public void Metadata_ExposesDangerousMutationAndCodeContract ()
        {
            var operation = CreateCsEvalOperation();

            Assert.That(operation.Metadata.OperationName, Is.EqualTo(UcliPrimitiveOperationNames.CsEval));
            Assert.That(operation.Metadata.Kind, Is.EqualTo(UcliOperationKind.Mutation));
            Assert.That(operation.Metadata.Policy, Is.EqualTo(OperationPolicy.Dangerous));
            Assert.That(operation.Metadata.RequiresPreCallPlanReplay, Is.True);
            Assert.That(operation.Metadata.DescribeContract.CodeContract, Is.Not.Null);
            Assert.That(operation.Metadata.DescribeContract.CodeContract!.EntryPoint!.Signature, Is.EqualTo("public static object? | Task | Task<T> | ValueTask | ValueTask<T> Run(UcliCsEvalContext context)"));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.EntryPoint.MatchRule, Is.EqualTo("Compiled source must contain exactly one public static Run(UcliCsEvalContext context) method returning object?, Task, Task<T>, ValueTask, or ValueTask<T>."));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.SourceForms![0].Kind, Is.EqualTo(CsEvalSourceKindValues.CompilationUnit));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.SourceForms[0].Description, Does.Contain("compilation unit"));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.SourceForms[1].Kind, Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.SourceForms[1].Description, Does.Contain("snippet"));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.EntryPoint.ReturnValue, Does.Contain("getter"));
            Assert.That(operation.Metadata.DescribeContract.CodeContract.ApiTypes!.Count, Is.EqualTo(1));
            var contextType = operation.Metadata.DescribeContract.CodeContract.ApiTypes![0];
            Assert.That(contextType.FullName, Is.EqualTo(typeof(UcliCsEvalContext).FullName));
            Assert.That(contextType.Members!.Count, Is.EqualTo(8));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.Log)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.LogWarning)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.LogError)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.DeclareNoTouchedResources)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.DeclareTouchedAsset)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.DeclareTouchedScene)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.DeclareTouchedPrefab)));
            Assert.That(contextType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == nameof(UcliCsEvalContext.DeclareTouchedProjectSettings)));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSourceIsValid_DoesNotInvokeEntryPoint () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
");

            var result = await operation.PlanAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.False);
            Assert.That(result.Changed, Is.False);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.CompilationUnit));
            Assert.That(payload.GetProperty("resolvedEntryPoint").GetString(), Is.EqualTo("EvalScripts.Entry.Run"));
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Succeeded));
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
            Assert.That(payload.TryGetProperty("logs", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenNullableContextDirectiveIsOmitted_DoesNotWarnForRequiredNullableSignature () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object? Run(UcliCsEvalContext context)
        {
            return null;
        }
    }
}
");

            var result = await operation.PlanAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Result!.Value.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.CompilationUnit));
            var diagnostics = result.Result!.Value.GetProperty("compile").GetProperty("diagnostics");
            for (var i = 0; i < diagnostics.GetArrayLength(); i++)
            {
                Assert.That(diagnostics[i].GetProperty("id").GetString(), Is.Not.EqualTo("CS8632"));
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PlanAndCall_WhenSourceIsSame_ReturnStableDigestValues () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            var source = @"
context.DeclareNoTouchedResources();
return new { value = 7 };
";
            using var planContext = new OperationExecutionContext();
            using var callContext = new OperationExecutionContext();

            var firstPlan = await operation.PlanAsync(CreateOperation(source), planContext, CancellationToken.None);
            var secondPlan = await operation.PlanAsync(CreateOperation(source), planContext, CancellationToken.None);
            var call = await operation.CallAsync(CreateOperation(source), callContext, CancellationToken.None);

            Assert.That(firstPlan.IsSuccess, Is.True);
            Assert.That(secondPlan.IsSuccess, Is.True);
            Assert.That(call.IsSuccess, Is.True);
            var firstPlanPayload = firstPlan.Result!.Value;
            var secondPlanPayload = secondPlan.Result!.Value;
            var callPayload = call.Result!.Value;
            var sourceDigest = firstPlanPayload.GetProperty("sourceDigest").GetString();
            var executionDigest = firstPlanPayload.GetProperty("executionDigest").GetString();
            Assert.That(sourceDigest, Has.Length.EqualTo(64));
            Assert.That(executionDigest, Has.Length.EqualTo(64));
            Assert.That(secondPlanPayload.GetProperty("sourceDigest").GetString(), Is.EqualTo(sourceDigest));
            Assert.That(secondPlanPayload.GetProperty("executionDigest").GetString(), Is.EqualTo(executionDigest));
            Assert.That(callPayload.GetProperty("sourceDigest").GetString(), Is.EqualTo(sourceDigest));
            Assert.That(callPayload.GetProperty("executionDigest").GetString(), Is.EqualTo(executionDigest));
        });

        [Test]
        [Category("Size.Small")]
        public void SourcePreparer_WhenSnippetUsesAwait_UsesAsyncWrapperVersion ()
        {
            var preparer = new CsEvalSourcePreparer();

            Assert.That(preparer.TryCreateSnippet("return 1;", out var syncSnippet, out _), Is.True);
            Assert.That(preparer.TryCreateSnippet(
                @"using System.Threading.Tasks;
await Task.Yield();
return 1;",
                out var asyncSnippet,
                out _), Is.True);

            Assert.That(syncSnippet.WrapperVersion, Is.EqualTo(CsEvalSourcePreparer.SnippetWrapperVersion));
            Assert.That(asyncSnippet.WrapperVersion, Is.EqualTo(CsEvalSourcePreparer.AsyncSnippetWrapperVersion));
            Assert.That(asyncSnippet.CompilationSource, Does.Contain("async System.Threading.Tasks.Task<object?> Run"));
        }

        [Test]
        [Category("Size.Small")]
        public void ExecutionDigest_WhenSourceKindOrWrapperDiffers_ChangesDigest ()
        {
            var sourceDigest = new string('a', 64);
            const string referencesIdentity = "refs";

            var compilationUnitDigest = CsEvalExecutionDigestCalculator.Compute(
                sourceDigest,
                CsEvalSourceKindValues.CompilationUnit,
                CsEvalSourcePreparer.NoWrapperVersion,
                referencesIdentity);
            var snippetDigest = CsEvalExecutionDigestCalculator.Compute(
                sourceDigest,
                CsEvalSourceKindValues.Snippet,
                CsEvalSourcePreparer.SnippetWrapperVersion,
                referencesIdentity);
            var asyncSnippetDigest = CsEvalExecutionDigestCalculator.Compute(
                sourceDigest,
                CsEvalSourceKindValues.Snippet,
                CsEvalSourcePreparer.AsyncSnippetWrapperVersion,
                referencesIdentity);

            Assert.That(compilationUnitDigest, Is.Not.EqualTo(snippetDigest));
            Assert.That(asyncSnippetDigest, Is.Not.EqualTo(snippetDigest));
            Assert.That(asyncSnippetDigest, Is.Not.EqualTo(compilationUnitDigest));
            Assert.That(compilationUnitDigest, Has.Length.EqualTo(64));
            Assert.That(snippetDigest, Has.Length.EqualTo(64));
            Assert.That(asyncSnippetDigest, Has.Length.EqualTo(64));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenStatementSnippetReturnsValue_ReturnsSnippetSourceKind () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
context.Log(""snippet"");
context.DeclareNoTouchedResources();
return new { count = 2 };
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(payload.GetProperty("resolvedEntryPoint").GetString(), Is.EqualTo("MackySoft.Ucli.Unity.Execution.CsEval.Generated.UcliCsEvalSnippetEntry.Run"));
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("snippet"));
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Json));
            Assert.That(payload.GetProperty("returnValue").GetProperty("value").GetProperty("count").GetInt32(), Is.EqualTo(2));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSnippetHasNoReturn_ReturnsNull () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
context.Log(""no return"");
context.DeclareNoTouchedResources();
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSnippetIsSingleExpression_ReturnsExpressionValue () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: "new { ok = true, label = \"expr\" }");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var value = result.Result!.Value.GetProperty("returnValue").GetProperty("value");
            Assert.That(value.GetProperty("ok").GetBoolean(), Is.True);
            Assert.That(value.GetProperty("label").GetString(), Is.EqualTo("expr"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSnippetHasLeadingUsing_CompilesWithUsing () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System.Text;

context.DeclareNoTouchedResources();
return new { value = new StringBuilder().Append(""ok"").ToString() };
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var value = result.Result!.Value.GetProperty("returnValue").GetProperty("value");
            Assert.That(value.GetProperty("value").GetString(), Is.EqualTo("ok"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSnippetUsesAwait_AwaitsAndSerializesResult () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System.Threading.Tasks;

context.Log(""before await"");
await Task.Yield();
context.DeclareNoTouchedResources();
return new { value = await Task.FromResult(5) };
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("before await"));
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Json));
            Assert.That(payload.GetProperty("returnValue").GetProperty("value").GetProperty("value").GetInt32(), Is.EqualTo(5));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenAwaitExpressionSnippetIsSingleExpression_ReturnsExpressionValue () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System.Threading.Tasks;

await Task.FromResult(9)
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Json));
            Assert.That(payload.GetProperty("returnValue").GetProperty("value").GetInt32(), Is.EqualTo(9));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenAwaitSnippetHasNoReturn_ReturnsNull () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System.Threading.Tasks;

await Task.Yield();
context.DeclareNoTouchedResources();
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Null));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSnippetDoesNotCompile_ReturnsSnippetDiagnosticLine () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"context.Log(""first"");
return missingSymbol;");

            var result = await operation.PlanAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.Snippet));
            var diagnostic = payload.GetProperty("compile").GetProperty("diagnostics")[0];
            Assert.That(diagnostic.GetProperty("line").GetInt32(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSourceDoesNotCompile_ReturnsDiagnostics () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
");

            var result = await operation.PlanAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.CompilationUnit));
            Assert.That(payload.TryGetProperty("resolvedEntryPoint", out _), Is.False);
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Failed));
            Assert.That(payload.GetProperty("compile").GetProperty("diagnostics").GetArrayLength(), Is.GreaterThan(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSourceReturnsJsonSerializableValue_ReturnsLogsAndTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(result.Touched[0].Path, Is.EqualTo("Assets/Eval.asset"));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.CompilationUnit));
            Assert.That(payload.GetProperty("resolvedEntryPoint").GetString(), Is.EqualTo("EvalScripts.Entry.Run"));
            Assert.That(payload.GetProperty("durationMilliseconds").GetInt64(), Is.GreaterThanOrEqualTo(0));
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("hello"));
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Json));
            Assert.That(payload.GetProperty("returnValue").GetProperty("value").GetProperty("count").GetInt32(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenNoTouchedResourcesDeclared_ReturnsNoneAndUnchanged () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
            context.DeclareNoTouchedResources();
            return null;
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Touched.Count, Is.EqualTo(0));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Null));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTouchedResourcesAreNotDeclared_ReturnsUnknownAndChanged () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
            return null;
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(0));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Null));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Unknown));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenPrefabAndProjectSettingsAreDeclared_ReturnsTypedTouches () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
            context.DeclareTouchedPrefab(""Assets/Widget.prefab"");
            context.DeclareTouchedProjectSettings(""ProjectSettings/ProjectSettings.asset"");
            return null;
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(2));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(result.Touched[0].Path, Is.EqualTo("Assets/Widget.prefab"));
            Assert.That(result.Touched[1].Kind, Is.EqualTo(OperationTouchKind.ProjectSettings));
            Assert.That(result.Touched[1].Path, Is.EqualTo("ProjectSettings/ProjectSettings.asset"));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("declared").GetArrayLength(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnValueCannotBeSerialized_FailsAfterInvocation () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
            context.DeclareTouchedAsset(""Assets/Eval.asset"");
            return typeof(string);
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Succeeded));
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnsTaskLikeObject_FailsAfterInvocation () => UniTask.ToCoroutine(async () =>
        {
            var cases = new[]
            {
                "System.Threading.Tasks.Task.CompletedTask",
                "System.Threading.Tasks.Task.FromResult(1)",
                "new System.Threading.Tasks.ValueTask()",
                "new System.Threading.Tasks.ValueTask<int>(1)",
            };
            var operation = CreateCsEvalOperation();
            for (var i = 0; i < cases.Length; i++)
            {
                using var context = new OperationExecutionContext();
                var request = CreateOperation(
                    source: @"
context.DeclareTouchedAsset(""Assets/Eval.asset"");
return " + cases[i] + @";
");

                var result = await operation.CallAsync(request, context, CancellationToken.None);

                AssertInvalidArgument(result, cases[i]);
                Assert.That(result.Applied, Is.True, cases[i]);
                Assert.That(result.Changed, Is.True, cases[i]);
                Assert.That(result.Touched.Count, Is.EqualTo(1), cases[i]);
                AssertReadInvalidations(
                    result,
                    new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                    new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                    new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
                var payload = result.Result!.Value;
                Assert.That(payload.TryGetProperty("returnValue", out _), Is.False, cases[i]);
                Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared), cases[i]);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnsDerivedTaskObject_FailsAfterInvocation () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public sealed class DerivedTask : Task
    {
        public DerivedTask () : base(() => { })
        {
        }
    }

    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.DeclareTouchedAsset(""Assets/Eval.asset"");
            return new DerivedTask();
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenEntryPointReturnTypeShadowsTask_FailsBeforeInvocation () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
namespace System.Threading.Tasks
{
    public sealed class Task<T>
    {
    }
}

namespace EvalScripts
{
    using MackySoft.Ucli.Unity.Execution.CsEval;

    public static class Entry
    {
        public static System.Threading.Tasks.Task<int> Run(UcliCsEvalContext context)
        {
            throw new System.InvalidOperationException(""must not invoke"");
        }
    }
}
");

            var result = await operation.PlanAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.False);
            Assert.That(result.Changed, Is.False);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Failed));
            var diagnostics = payload.GetProperty("compile").GetProperty("diagnostics");
            var diagnosticMessages = new string[diagnostics.GetArrayLength()];
            var hasShadowTaskDiagnostic = false;
            for (var i = 0; i < diagnosticMessages.Length; i++)
            {
                diagnosticMessages[i] = diagnostics[i].GetProperty("message").GetString()!;
                hasShadowTaskDiagnostic |= diagnosticMessages[i].Contains("conflicts with the imported type")
                    || diagnosticMessages[i].Contains("expected object, Task, Task<T>, ValueTask, or ValueTask<T>");
            }

            Assert.That(hasShadowTaskDiagnostic, Is.True, string.Join("\n", diagnosticMessages));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnsGenericTaskLikeValue_AwaitsAndSerializesResult () => UniTask.ToCoroutine(async () =>
        {
            var cases = new[]
            {
                new AsyncReturnCase(
                    "task-int",
                    "public static async System.Threading.Tasks.Task<int> Run(UcliCsEvalContext context)",
                    "await System.Threading.Tasks.Task.Yield(); return 7;",
                    JsonValueKind.Number,
                    7,
                    null),
                new AsyncReturnCase(
                    "value-task-string",
                    "public static async System.Threading.Tasks.ValueTask<string> Run(UcliCsEvalContext context)",
                    "await System.Threading.Tasks.Task.Yield(); return \"ok\";",
                    JsonValueKind.String,
                    null,
                    "ok"),
            };
            var operation = CreateCsEvalOperation();
            for (var i = 0; i < cases.Length; i++)
            {
                var testCase = cases[i];
                using var context = new OperationExecutionContext();
                var request = CreateOperation(CreateAsyncEntryPointSource(testCase.Signature, testCase.Body));

                var result = await operation.CallAsync(request, context, CancellationToken.None);

                Assert.That(result.IsSuccess, Is.True, testCase.Name);
                Assert.That(result.Applied, Is.True, testCase.Name);
                Assert.That(result.Changed, Is.False, testCase.Name);
                var value = result.Result!.Value.GetProperty("returnValue").GetProperty("value");
                Assert.That(value.ValueKind, Is.EqualTo(testCase.ExpectedValueKind), testCase.Name);
                if (testCase.ExpectedNumberValue.HasValue)
                {
                    Assert.That(value.GetInt32(), Is.EqualTo(testCase.ExpectedNumberValue.Value), testCase.Name);
                }

                if (testCase.ExpectedStringValue != null)
                {
                    Assert.That(value.GetString(), Is.EqualTo(testCase.ExpectedStringValue), testCase.Name);
                }

                Assert.That(result.Result!.Value.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None), testCase.Name);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTaskLikeReturnCompletesOffThread_SerializesResultOnEvalThread () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static Task<ThreadCheckedReturn> Run(UcliCsEvalContext context)
        {
            context.DeclareNoTouchedResources();
            var evalThreadId = Thread.CurrentThread.ManagedThreadId;
            return Task.Run(() => new ThreadCheckedReturn(evalThreadId));
        }
    }

    public sealed class ThreadCheckedReturn
    {
        private readonly int evalThreadId;

        public ThreadCheckedReturn(int evalThreadId)
        {
            this.evalThreadId = evalThreadId;
        }

        public string Value
        {
            get
            {
                if (Thread.CurrentThread.ManagedThreadId != evalThreadId)
                {
                    throw new InvalidOperationException(""getter ran outside eval thread"");
                }

                return ""ok"";
            }
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Json));
            Assert.That(payload.GetProperty("returnValue").GetProperty("value").GetProperty("value").GetString(), Is.EqualTo("ok"));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnsNonGenericTaskLike_CompletesWithNullReturnValue () => UniTask.ToCoroutine(async () =>
        {
            var cases = new[]
            {
                new AsyncVoidReturnCase(
                    "task",
                    "public static async System.Threading.Tasks.Task Run(UcliCsEvalContext context)",
                    "await System.Threading.Tasks.Task.Yield();"),
                new AsyncVoidReturnCase(
                    "value-task",
                    "public static async System.Threading.Tasks.ValueTask Run(UcliCsEvalContext context)",
                    "await System.Threading.Tasks.Task.Yield();"),
            };
            var operation = CreateCsEvalOperation();
            for (var i = 0; i < cases.Length; i++)
            {
                var testCase = cases[i];
                using var context = new OperationExecutionContext();
                var request = CreateOperation(CreateAsyncEntryPointSource(testCase.Signature, testCase.Body));

                var result = await operation.CallAsync(request, context, CancellationToken.None);

                Assert.That(result.IsSuccess, Is.True, testCase.Name);
                Assert.That(result.Applied, Is.True, testCase.Name);
                Assert.That(result.Changed, Is.False, testCase.Name);
                var payload = result.Result!.Value;
                Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Null), testCase.Name);
                Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None), testCase.Name);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnedTaskFaults_FailsAfterInvocationWithTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var cases = new[]
            {
                "public static async System.Threading.Tasks.Task Run(UcliCsEvalContext context)",
                "public static async System.Threading.Tasks.Task<int> Run(UcliCsEvalContext context)",
                "public static async System.Threading.Tasks.ValueTask Run(UcliCsEvalContext context)",
                "public static async System.Threading.Tasks.ValueTask<int> Run(UcliCsEvalContext context)",
            };
            var operation = CreateCsEvalOperation();
            for (var i = 0; i < cases.Length; i++)
            {
                using var context = new OperationExecutionContext();
                var request = CreateOperation(
                    source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        " + cases[i] + @"
        {
            context.LogWarning(""before fault"");
            context.DeclareTouchedAsset(""Assets/Eval.asset"");
            await System.Threading.Tasks.Task.Yield();
            throw new System.InvalidOperationException(""async boom"");
        }
    }
}
");

                var result = await operation.CallAsync(request, context, CancellationToken.None);

                AssertInvalidArgument(result, cases[i]);
                Assert.That(result.Applied, Is.True, cases[i]);
                Assert.That(result.Changed, Is.True, cases[i]);
                Assert.That(result.Touched.Count, Is.EqualTo(1), cases[i]);
                Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Asset), cases[i]);
                var payload = result.Result!.Value;
                Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("before fault"), cases[i]);
                Assert.That(payload.TryGetProperty("returnValue", out _), Is.False, cases[i]);
                Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared), cases[i]);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenReturnValueGetterThrows_FailsAfterInvocationWithTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public sealed class ThrowingReturn
    {
        public string Value
        {
            get { throw new System.NullReferenceException(""boom""); }
        }
    }

    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.DeclareTouchedAsset(""Assets/Eval.asset"");
            return new ThrowingReturn();
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSnippetReturnValueIsTooLarge_FailsBeforeIpcSerialization () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: "return new string('x', 9 * 1024 * 1024);");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("sourceKind").GetString(), Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSourceExceedsInternalGuardrail_FailsBeforeCompilation () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(new string('x', CsEvalSafetyLimits.MaxSourceBytes + 1));

            var result = await operation.PlanAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            var diagnostic = result.Result!.Value.GetProperty("compile").GetProperty("diagnostics")[0];
            Assert.That(diagnostic.GetProperty("id").GetString(), Is.EqualTo(CsEvalDiagnosticIds.SourceTooLarge));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenLogsExceedInternalGuardrail_TruncatesLogs () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(@"
for (var i = 0; i < " + (CsEvalSafetyLimits.MaxLogEntries + 1) + @"; i++)
{
    context.Log(""entry "" + i);
}
context.DeclareNoTouchedResources();
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var logs = result.Result!.Value.GetProperty("logs");
            Assert.That(logs.GetArrayLength(), Is.EqualTo(CsEvalSafetyLimits.MaxLogEntries));
            Assert.That(logs[logs.GetArrayLength() - 1].GetProperty("level").GetString(), Is.EqualTo(CsEvalLogLevelValues.Warning));
            Assert.That(logs[logs.GetArrayLength() - 1].GetProperty("message").GetString(), Does.Contain("truncated"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTouchedResourcesExceedInternalGuardrail_ReturnsUnknownAuditState () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(@"
for (var i = 0; i < " + (CsEvalSafetyLimits.MaxTouchedResources + 1) + @"; i++)
{
    context.DeclareTouchedAsset(""Assets/Eval"" + i + "".asset"");
}
return null;
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Unknown));
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointThrows_FailsAfterInvocationWithDeclaredState () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using System;
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.LogWarning(""before throw"");
            context.DeclareTouchedScene(""Assets/Main.unity"");
            throw new InvalidOperationException(""boom"");
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Scene));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("before throw"));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointSignatureIsInvalid_FailsBeforeInvocation () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            var cases = new[]
            {
                new InvalidEntryPointCase(
                    "private",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        private static object Run(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
            return null;
        }
    }
}
"),
                new InvalidEntryPointCase(
                    "instance",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public sealed class Entry
    {
        public object Run(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
            return null;
        }
    }
}
"),
                new InvalidEntryPointCase(
                    "no args",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run()
        {
            return null;
        }
    }
}
"),
                new InvalidEntryPointCase(
                    "wrong arg",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run(string context)
        {
            return null;
        }
    }
}
"),
                new InvalidEntryPointCase(
                    "generic",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run<T>(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
            return null;
        }
    }
}
"),
                new InvalidEntryPointCase(
                    "void",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static void Run(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
        }
    }
}
"),
                new InvalidEntryPointCase(
                    "string",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static string Run(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
            return ""value"";
        }
    }
}
"),
                new InvalidEntryPointCase(
                    "wrong method",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Execute(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
            return null;
        }
    }
}
"),
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var testCase = cases[i];
                using var context = new OperationExecutionContext();
                var result = await operation.CallAsync(CreateOperation(testCase.Source), context, CancellationToken.None);

                AssertInvalidArgument(result, testCase.Name);
                Assert.That(result.Applied, Is.False, testCase.Name);
                Assert.That(result.Changed, Is.False, testCase.Name);
                var payload = result.Result!.Value;
                Assert.That(payload.TryGetProperty("resolvedEntryPoint", out _), Is.False, testCase.Name);
                Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Failed), testCase.Name);
                if (string.Equals(testCase.Name, "private", System.StringComparison.Ordinal))
                {
                    Assert.That(
                        payload.GetProperty("compile").GetProperty("diagnostics")[0].GetProperty("id").GetString(),
                        Is.EqualTo(CsEvalDiagnosticIds.EntryPointRejected));
                }

                Assert.That(payload.TryGetProperty("logs", out _), Is.False, testCase.Name);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenMultipleEntryPointsMatch_FailsBeforeInvocationWithCandidates () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
            using var context = new OperationExecutionContext();
            var request = CreateOperation(
                source: @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class First
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.Log(""first"");
            return new object();
        }
    }

    public static class Second
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.Log(""second"");
            return new object();
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.False);
            Assert.That(result.Changed, Is.False);
            var payload = result.Result!.Value;
            Assert.That(payload.TryGetProperty("resolvedEntryPoint", out _), Is.False);
            var diagnosticMessage = payload
                .GetProperty("compile")
                .GetProperty("diagnostics")[0]
                .GetProperty("message")
                .GetString();
            Assert.That(payload.GetProperty("compile").GetProperty("diagnostics")[0].GetProperty("id").GetString(), Is.EqualTo(CsEvalDiagnosticIds.EntryPointAmbiguous));
            Assert.That(diagnosticMessage, Does.Contain("EvalScripts.First.Run"));
            Assert.That(diagnosticMessage, Does.Contain("EvalScripts.Second.Run"));
            Assert.That(payload.TryGetProperty("logs", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTouchedResourcePathIsInvalid_FailsAfterInvocation () => UniTask.ToCoroutine(async () =>
        {
            var cases = new[]
            {
                "context.DeclareTouchedAsset(\"Assets/../Eval.asset\");",
                "context.DeclareTouchedScene(\"Assets/Main.prefab\");",
                "context.DeclareTouchedPrefab(\"/Assets/Main.prefab\");",
                "context.DeclareTouchedProjectSettings(\"ProjectSettings.asset\");",
                "context.DeclareTouchedAsset(\"Assets/Foo:Bar.asset\");",
                "context.DeclareTouchedProjectSettings(\"ProjectSettings/Foo:Bar.asset\");",
                "context.DeclareTouchedAsset(\"Assets/Main.unity\");",
                "context.DeclareTouchedAsset(\"Assets/Widget.prefab\");",
                "context.DeclareTouchedAsset(\"Assets/Main.UNITY\");",
                "context.DeclareTouchedAsset(\"Assets/Widget.PREFAB\");",
            };
            var operation = CreateCsEvalOperation();
            for (var i = 0; i < cases.Length; i++)
            {
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
            " + cases[i] + @"
            return null;
        }
    }
}
");

                var result = await operation.CallAsync(request, context, CancellationToken.None);

                AssertInvalidArgument(result, cases[i]);
                Assert.That(result.Applied, Is.True, cases[i]);
                Assert.That(result.Changed, Is.True, cases[i]);
                Assert.That(result.Touched.Count, Is.EqualTo(0), cases[i]);
                AssertReadInvalidations(
                    result,
                    new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                    new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null),
                    new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, ScenePath: null));
                var payload = result.Result!.Value;
                Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Unknown), cases[i]);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnsTaskObjectResult_FailsAfterInvocation () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
            context.DeclareTouchedAsset(""Assets/Eval.asset"");
            return Task.FromResult<object>(Task.CompletedTask);
        }
    }
}
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Succeeded));
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTouchedDeclarationConflicts_FailsWithLogs () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateCsEvalOperation();
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
");

            var result = await operation.CallAsync(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.False);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("before conflict"));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        private static CsEvalOperation CreateCsEvalOperation ()
        {
            return new CsEvalOperation(
                new CsEvalCompilationService(
                    new CsEvalReferenceResolver(),
                    new CsEvalEntryPointSymbolValidator(),
                    new CsEvalSourcePreparer()),
                new CsEvalEntryPointReflectionResolver(),
                new CsEvalReturnValueSerializer());
        }

        private static NormalizedOperation CreateOperation (
            string source)
        {
            return new NormalizedOperation(
                Id: "cs-eval",
                Op: UcliPrimitiveOperationNames.CsEval,
                Args: JsonSerializer.SerializeToElement(new
                {
                    source,
                }),
                As: null,
                Expect: null);
        }

        private static string CreateAsyncEntryPointSource (
            string signature,
            string body)
        {
            return @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        " + signature + @"
        {
            context.DeclareNoTouchedResources();
            " + body + @"
        }
    }
}
";
        }

        private static void AssertInvalidArgument (
            OperationPhaseStepResult result,
            string? message = null)
        {
            Assert.That(result.IsSuccess, Is.False, message);
            Assert.That(result.Failure, Is.Not.Null, message);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument), message);
        }

        private static void AssertReadInvalidations (
            OperationPhaseStepResult result,
            params OperationReadInvalidation[] expected)
        {
            Assert.That(result.ReadInvalidations.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(result.ReadInvalidations[i].Surface, Is.EqualTo(expected[i].Surface));
                Assert.That(result.ReadInvalidations[i].ScenePath, Is.EqualTo(expected[i].ScenePath));
            }
        }

        private sealed class InvalidEntryPointCase
        {
            public InvalidEntryPointCase (
                string name,
                string source)
            {
                Name = name;
                Source = source;
            }

            public string Name { get; }

            public string Source { get; }
        }

        private sealed class AsyncReturnCase
        {
            public AsyncReturnCase (
                string name,
                string signature,
                string body,
                JsonValueKind expectedValueKind,
                int? expectedNumberValue,
                string? expectedStringValue)
            {
                Name = name;
                Signature = signature;
                Body = body;
                ExpectedValueKind = expectedValueKind;
                ExpectedNumberValue = expectedNumberValue;
                ExpectedStringValue = expectedStringValue;
            }

            public string Name { get; }

            public string Signature { get; }

            public string Body { get; }

            public JsonValueKind ExpectedValueKind { get; }

            public int? ExpectedNumberValue { get; }

            public string? ExpectedStringValue { get; }
        }

        private sealed class AsyncVoidReturnCase
        {
            public AsyncVoidReturnCase (
                string name,
                string signature,
                string body)
            {
                Name = name;
                Signature = signature;
                Body = body;
            }

            public string Name { get; }

            public string Signature { get; }

            public string Body { get; }
        }
    }
}
