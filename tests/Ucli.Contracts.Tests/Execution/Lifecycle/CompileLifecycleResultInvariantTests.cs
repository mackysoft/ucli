using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;

public sealed class CompileLifecycleResultInvariantTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0, Verdict.Pass)]
    [InlineData(1, Verdict.Fail)]
    public void CompileVerdictPolicy_EvaluatesTypedCompileEvidence (
        int errorCount,
        Verdict expected)
    {
        var result = LifecycleExecutionContractTestFactory.CreateCompileResult(
            errorCount);

        Assert.Equal(expected, CompileLifecycleVerdictPolicy.Evaluate(result));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileVerdictPolicy_WhenRequiredObservationIsMissing_ReturnsIncomplete ()
    {
        var complete = LifecycleExecutionContractTestFactory.CreateCompileResult();
        var incompleteResults = new CompileLifecycleResult[]
        {
            new(
                new CompileLifecycleResult.RefreshEvidence(
                    complete.Refresh.Origin,
                    complete.Refresh.Requested,
                    complete.Refresh.StartedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                complete.ScriptCompilation,
                complete.DomainReload,
                complete.Lifecycle),
            new(
                complete.Refresh,
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    complete.ScriptCompilation.Started,
                    Completed: false,
                    complete.ScriptCompilation.CompileGenerationBefore,
                    complete.ScriptCompilation.CompileGenerationAfter,
                    complete.ScriptCompilation.Diagnostics),
                complete.DomainReload,
                complete.Lifecycle),
            new(
                complete.Refresh,
                complete.ScriptCompilation,
                new CompileLifecycleResult.DomainReloadEvidence(
                    complete.DomainReload.ReloadRequired,
                    complete.DomainReload.ReloadObserved,
                    complete.DomainReload.GenerationBefore,
                    complete.DomainReload.GenerationAfter,
                    Settled: false),
                complete.Lifecycle),
            new(
                complete.Refresh,
                complete.ScriptCompilation,
                complete.DomainReload,
                new CompileLifecycleResult.LifecycleEvidence(
                    complete.Lifecycle.ServerVersion,
                    complete.Lifecycle.UnityVersion,
                    State: null,
                    complete.Lifecycle.ObservedAtUtc,
                    complete.Lifecycle.ActionRequired,
                    complete.Lifecycle.PrimaryDiagnostic)),
        };

        Assert.All(
            incompleteResults,
            result => Assert.Equal(
                Verdict.Incomplete,
                CompileLifecycleVerdictPolicy.Evaluate(result)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileVerdictPolicy_WhenObservedLifecycleIsNotReady_ReturnsFail ()
    {
        var complete = LifecycleExecutionContractTestFactory.CreateCompileResult();
        var readyState = complete.Lifecycle.State!;
        var notReadyState = new UnityEditorStateSnapshot(
            readyState.EditorMode,
            UnityEditorLifecycleState.Busy,
            readyState.CompileState,
            readyState.Generations,
            readyState.PlayMode);
        var result = new CompileLifecycleResult(
            complete.Refresh,
            complete.ScriptCompilation,
            complete.DomainReload,
            new CompileLifecycleResult.LifecycleEvidence(
                complete.Lifecycle.ServerVersion,
                complete.Lifecycle.UnityVersion,
                notReadyState,
                complete.Lifecycle.ObservedAtUtc,
                complete.Lifecycle.ActionRequired,
                complete.Lifecycle.PrimaryDiagnostic));

        Assert.Equal(Verdict.Fail, CompileLifecycleVerdictPolicy.Evaluate(result));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RefreshEvidence_WhenCompletionStateOrTimestampIsInvalid_RejectsValue ()
    {
        Assert.Equal(
            "CompletedAtUtc",
            Assert.Throws<ArgumentException>(() => new CompileLifecycleResult.RefreshEvidence(
                CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc,
                CompletedAtUtc: null,
                Completed: true)).ParamName);
        Assert.Equal(
            "StartedAtUtc",
            Assert.Throws<ArgumentException>(() => new CompileLifecycleResult.RefreshEvidence(
                CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc.ToOffset(TimeSpan.FromHours(1)),
                CompletedAtUtc: null,
                Completed: false)).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ScriptCompilationEvidence_WhenNestedContractIsInvalid_RejectsValue ()
    {
        Assert.Equal(
            "Diagnostics",
            Assert.Throws<ArgumentNullException>(() =>
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    CompileGenerationBefore: null,
                    CompileGenerationAfter: null,
                    Diagnostics: null!)).ParamName);
        Assert.Equal(
            "CompileGenerationBefore",
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    CompileGenerationBefore: -1,
                    CompileGenerationAfter: null,
                    Diagnostics: new CompileLifecycleResult.DiagnosticsEvidence(
                        0,
                        0,
                        null))).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ScriptCompilationEvidence_WhenGenerationRegresses_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompileLifecycleResult.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 4,
                CompileGenerationAfter: 3,
                Diagnostics: new CompileLifecycleResult.DiagnosticsEvidence(
                    0,
                    0,
                    null)));

        Assert.Equal("CompileGenerationAfter", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ScriptCompilationEvidence_WhenCompletionWasObservedWithoutStartEvent_AcceptsValue ()
    {
        var evidence = new CompileLifecycleResult.ScriptCompilationEvidence(
            Started: false,
            Completed: true,
            CompileGenerationBefore: 4,
            CompileGenerationAfter: 4,
            Diagnostics: new CompileLifecycleResult.DiagnosticsEvidence(
                0,
                0,
                null));

        Assert.False(evidence.Started);
        Assert.True(evidence.Completed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DiagnosticsAndDomainReloadEvidence_WhenCountsOrGenerationsAreNegative_RejectValues ()
    {
        Assert.Equal(
            "ErrorCount",
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CompileLifecycleResult.DiagnosticsEvidence(-1, 0, null)).ParamName);
        Assert.Equal(
            "GenerationAfter",
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CompileLifecycleResult.DomainReloadEvidence(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: null,
                    GenerationAfter: -1,
                    Settled: false)).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DomainReloadEvidence_WhenGenerationRegresses_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompileLifecycleResult.DomainReloadEvidence(
                ReloadRequired: true,
                ReloadObserved: true,
                GenerationBefore: 4,
                GenerationAfter: 3,
                Settled: true));

        Assert.Equal("GenerationAfter", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleEvidence_WhenObservedTimestampIsNotUtc_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CompileLifecycleResult.LifecycleEvidence(
                ServerVersion: null,
                UnityVersion: null,
                State: null,
                ObservedAtUtc: StartedAtUtc.ToOffset(TimeSpan.FromHours(9)),
                ActionRequired: null,
                PrimaryDiagnostic: null));

        Assert.Equal("ObservedAtUtc", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(true, "")]
    [InlineData(true, " server")]
    [InlineData(false, " ")]
    [InlineData(false, "unity ")]
    public void LifecycleEvidence_WhenPresentVersionIsNotCanonical_RejectsValue (
        bool serverVersion,
        string invalidValue)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CompileLifecycleResult.LifecycleEvidence(
                ServerVersion: serverVersion ? invalidValue : null,
                UnityVersion: serverVersion ? null : invalidValue,
                State: null,
                ObservedAtUtc: StartedAtUtc,
                ActionRequired: null,
                PrimaryDiagnostic: null));

        Assert.Equal(serverVersion ? "ServerVersion" : "UnityVersion", exception.ParamName);
    }
}
