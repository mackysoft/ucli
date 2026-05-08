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
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null));
            var payload = result.Result!.Value;
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
            context.DeclareNoTouchedResources();
            return null;
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Touched.Count, Is.EqualTo(0));
            AssertReadInvalidations(result);
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("returnValue").GetProperty("kind").GetString(), Is.EqualTo(CsEvalReturnValueKindValues.Null));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.None));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTouchedResourcesAreNotDeclared_ReturnsUnknownAndChanged () => UniTask.ToCoroutine(async () =>
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
            return null;
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

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
            context.DeclareTouchedPrefab(""Assets/Widget.prefab"");
            context.DeclareTouchedProjectSettings(""ProjectSettings/ProjectSettings.asset"");
            return null;
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

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
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("declared").GetArrayLength(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointReturnValueCannotBeSerialized_FailsAfterInvocation () => UniTask.ToCoroutine(async () =>
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
            context.DeclareTouchedAsset(""Assets/Eval.asset"");
            return typeof(string);
        }
    }
}
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                new OperationReadInvalidation(OperationReadInvalidationSurface.GuidPath, ScenePath: null));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Succeeded));
            Assert.That(payload.TryGetProperty("returnValue", out _), Is.False);
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointThrows_FailsAfterInvocationWithDeclaredState () => UniTask.ToCoroutine(async () =>
        {
            var operation = new CsEvalOperation();
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
",
                entryPoint: "EvalScripts.Entry.Run");

            var result = await operation.Call(request, context, CancellationToken.None);

            AssertInvalidArgument(result);
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Scene));
            AssertReadInvalidations(
                result,
                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, "Assets/Main.unity"));
            var payload = result.Result!.Value;
            Assert.That(payload.GetProperty("logs")[0].GetProperty("message").GetString(), Is.EqualTo("before throw"));
            Assert.That(payload.GetProperty("touchedResources").GetProperty("state").GetString(), Is.EqualTo(CsEvalTouchedResourceStateValues.Declared));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenEntryPointSignatureIsInvalid_FailsBeforeInvocation () => UniTask.ToCoroutine(async () =>
        {
            var operation = new CsEvalOperation();
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
",
                    "EvalScripts.Entry.Run"),
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
",
                    "EvalScripts.Entry.Run"),
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
",
                    "EvalScripts.Entry.Run"),
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
",
                    "EvalScripts.Entry.Run"),
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
",
                    "EvalScripts.Entry.Run"),
                new InvalidEntryPointCase(
                    "value task",
                    @"
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static ValueTask<object> Run(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
            return new ValueTask<object>((object)null);
        }
    }
}
",
                    "EvalScripts.Entry.Run"),
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
",
                    "EvalScripts.Entry.Run"),
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
",
                    "EvalScripts.Entry.Run"),
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
",
                    "EvalScripts.Entry.Execute"),
                new InvalidEntryPointCase(
                    "invalid format",
                    @"
using MackySoft.Ucli.Unity.Execution.CsEval;

namespace EvalScripts
{
    public static class Entry
    {
        public static object Run(UcliCsEvalContext context)
        {
            context.Log(""invoked"");
            return null;
        }
    }
}
",
                    "Entry.Run"),
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var testCase = cases[i];
                using var context = new OperationExecutionContext();
                var result = await operation.Call(CreateOperation(testCase.Source, testCase.EntryPoint), context, CancellationToken.None);

                AssertInvalidArgument(result, testCase.Name);
                Assert.That(result.Applied, Is.False, testCase.Name);
                Assert.That(result.Changed, Is.False, testCase.Name);
                var payload = result.Result!.Value;
                Assert.That(payload.GetProperty("compile").GetProperty("status").GetString(), Is.EqualTo(CsEvalCompileStatusValues.Failed), testCase.Name);
                Assert.That(payload.TryGetProperty("logs", out _), Is.False, testCase.Name);
            }
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
                "context.DeclareTouchedAsset(\"Assets/Main.unity\");",
                "context.DeclareTouchedAsset(\"Assets/Widget.prefab\");",
            };
            var operation = new CsEvalOperation();
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
",
                    entryPoint: "EvalScripts.Entry.Run");

                var result = await operation.Call(request, context, CancellationToken.None);

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
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Changed, Is.False);
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
                string source,
                string entryPoint)
            {
                Name = name;
                Source = source;
                EntryPoint = entryPoint;
            }

            public string Name { get; }

            public string Source { get; }

            public string EntryPoint { get; }
        }
    }
}
