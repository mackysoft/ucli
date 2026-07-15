using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Protocol;

public sealed class IpcCompileSummaryInvariantTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCompletionStateAndTimestampDisagree_RejectsValue ()
    {
        Assert.Equal(
            "CompletedAtUtc",
            Assert.Throws<ArgumentException>(() => CreateSummary(
                completed: true,
                completedAtUtc: null)).ParamName);
        Assert.Equal(
            "CompletedAtUtc",
            Assert.Throws<ArgumentException>(() => CreateSummary(
                completed: false,
                completedAtUtc: StartedAtUtc)).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenTimestampsAreNotCanonicalUtcOrOrdered_RejectsValue ()
    {
        var nonUtcTimestamp = new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(9));
        Assert.Equal(
            "StartedAtUtc",
            Assert.Throws<ArgumentException>(() => CreateSummary(
                completed: false,
                completedAtUtc: null,
                startedAtUtc: nonUtcTimestamp)).ParamName);
        Assert.Equal(
            "CompletedAtUtc",
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSummary(
                completed: true,
                completedAtUtc: StartedAtUtc.AddTicks(-1))).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RefreshEvidence_WhenCompletionStateOrTimestampIsInvalid_RejectsValue ()
    {
        Assert.Equal(
            "CompletedAtUtc",
            Assert.Throws<ArgumentException>(() => new IpcCompileSummary.RefreshEvidence(
                CompileRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc,
                CompletedAtUtc: null,
                Completed: true)).ParamName);
        Assert.Equal(
            "StartedAtUtc",
            Assert.Throws<ArgumentException>(() => new IpcCompileSummary.RefreshEvidence(
                CompileRefreshOrigin.AssetDatabaseRefresh,
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
            Assert.Throws<ArgumentNullException>(() => new IpcCompileSummary.ScriptCompilationEvidence(
                Started: false,
                Completed: false,
                CompileGenerationBefore: null,
                CompileGenerationAfter: null,
                Diagnostics: null!)).ParamName);
        Assert.Equal(
            "CompileGenerationBefore",
            Assert.Throws<ArgumentOutOfRangeException>(() => new IpcCompileSummary.ScriptCompilationEvidence(
                Started: false,
                Completed: false,
                CompileGenerationBefore: -1,
                CompileGenerationAfter: null,
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(0, 0, null))).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DiagnosticsAndDomainReloadEvidence_WhenCountsOrGenerationsAreNegative_RejectValues ()
    {
        Assert.Equal(
            "ErrorCount",
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new IpcCompileSummary.DiagnosticsEvidence(-1, 0, null)).ParamName);
        Assert.Equal(
            "GenerationAfter",
            Assert.Throws<ArgumentOutOfRangeException>(() => new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: null,
                GenerationAfter: -1,
                Settled: false)).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleEvidence_WhenObservedTimestampIsNotUtc_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcCompileSummary.LifecycleEvidence(
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
        var exception = Assert.Throws<ArgumentException>(() => new IpcCompileSummary.LifecycleEvidence(
            ServerVersion: serverVersion ? invalidValue : null,
            UnityVersion: serverVersion ? null : invalidValue,
            State: null,
            ObservedAtUtc: StartedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null));

        Assert.Equal(serverVersion ? "ServerVersion" : "UnityVersion", exception.ParamName);
    }

    private static IpcCompileSummary CreateSummary (
        bool completed,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset? startedAtUtc = null)
    {
        var effectiveStartedAtUtc = startedAtUtc ?? StartedAtUtc;
        return new IpcCompileSummary(
            RunId: Guid.Parse("b25b90f6-0b26-4372-99a4-262e784ccfae"),
            ProjectFingerprint: new ProjectFingerprint(
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            Completed: completed,
            StartedAtUtc: effectiveStartedAtUtc,
            CompletedAtUtc: completedAtUtc,
            Refresh: new IpcCompileSummary.RefreshEvidence(
                CompileRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc,
                CompletedAtUtc: null,
                Completed: false),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: false,
                Completed: false,
                CompileGenerationBefore: 0,
                CompileGenerationAfter: 0,
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(0, 0, null)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 0,
                GenerationAfter: 0,
                Settled: false),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: null,
                UnityVersion: null,
                State: null,
                ObservedAtUtc: StartedAtUtc,
                ActionRequired: null,
                PrimaryDiagnostic: null));
    }
}
