using System.Collections.ObjectModel;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;

public sealed class AssuranceReportReferenceTests
{
    public static TheoryData<string?> InvalidLocators => new()
    {
        null,
        string.Empty,
        " ",
        " report.json",
        "report.json ",
        "\treport.json",
        "report.json\r\n",
    };

    [Fact]
    [Trait("Size", "Small")]
    public void FromPath_WithValidLocator_SetsOnlyPathAndPreservesDigest ()
    {
        var digest = Sha256Digest.Parse(new string('a', 64));

        var reference = AssuranceReportReference.FromPath("artifacts/report.json", digest);

        Assert.Equal("artifacts/report.json", reference.Path);
        Assert.Null(reference.Uri);
        Assert.Same(digest, reference.Digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromUri_WithValidLocator_SetsOnlyUriAndPreservesDigest ()
    {
        var digest = Sha256Digest.Parse(new string('b', 64));

        var reference = AssuranceReportReference.FromUri("ucli://logs/unity?tail=200", digest);

        Assert.Null(reference.Path);
        Assert.Equal("ucli://logs/unity?tail=200", reference.Uri);
        Assert.Same(digest, reference.Digest);
    }

    [Theory]
    [MemberData(nameof(InvalidLocators))]
    [Trait("Size", "Small")]
    public void Factories_WithInvalidLocator_ThrowArgumentException (string? locator)
    {
        var pathException = Assert.ThrowsAny<ArgumentException>(
            () => AssuranceReportReference.FromPath(locator!, digest: null));
        var uriException = Assert.ThrowsAny<ArgumentException>(
            () => AssuranceReportReference.FromUri(locator!, digest: null));

        Assert.Equal("path", pathException.ParamName);
        Assert.Equal("uri", uriException.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StringKeyedExecutionOutputs_ExposeOrdinalReadOnlyReportSnapshots ()
    {
        var report = AssuranceReportReference.FromPath("artifacts/report.json", digest: null);
        var source = new Dictionary<string, AssuranceReportReference>(StringComparer.OrdinalIgnoreCase)
        {
            ["report"] = report,
        };
        var snapshots = CreateReportSnapshots(source);

        source.Add("late", report);

        Assert.All(snapshots, snapshot =>
        {
            var readOnly = Assert.IsType<ReadOnlyDictionary<string, AssuranceReportReference>>(snapshot);
            Assert.Same(report, readOnly["report"]);
            Assert.False(readOnly.ContainsKey("REPORT"));
            Assert.False(readOnly.ContainsKey("late"));
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, AssuranceReportReference>)readOnly).Add("other", report));
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildExecutionOutput_ExposesTypedReadOnlyReportSnapshot ()
    {
        var report = AssuranceReportReference.FromPath("artifacts/report.json", digest: null);
        var source = new Dictionary<BuildArtifactKind, AssuranceReportReference>
        {
            [BuildArtifactKind.Build] = report,
        };
        var output = new BuildExecutionOutput(
            Verdict: AssuranceVerdict.Pass,
            Project: ProjectIdentityInfoTestFactory.Create(),
            Build: AssuranceExecutionOutputTestFactory.CreateBuildOutput(),
            Verifiers: [],
            Claims: [],
            Reports: source,
            ResidualRisks: []);

        source.Add(BuildArtifactKind.BuildLog, report);

        var snapshot = Assert.IsType<ReadOnlyDictionary<BuildArtifactKind, AssuranceReportReference>>(output.Reports);
        Assert.Same(report, snapshot[BuildArtifactKind.Build]);
        Assert.False(snapshot.ContainsKey(BuildArtifactKind.BuildLog));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<BuildArtifactKind, AssuranceReportReference>)snapshot).Add(BuildArtifactKind.BuildReport, report));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildExecutionOutput_WithUndefinedReportKey_ThrowsArgumentException ()
    {
        var reports = new Dictionary<BuildArtifactKind, AssuranceReportReference>
        {
            [default] = AssuranceReportReference.FromPath("artifacts/report.json", digest: null),
        };

        var exception = Assert.Throws<ArgumentException>(() => new BuildExecutionOutput(
            Verdict: AssuranceVerdict.Pass,
            Project: ProjectIdentityInfoTestFactory.Create(),
            Build: AssuranceExecutionOutputTestFactory.CreateBuildOutput(),
            Verifiers: [],
            Claims: [],
            Reports: reports,
            ResidualRisks: []));

        Assert.Equal("Reports", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_WithNullReports_ThrowArgumentNullException ()
    {
        var constructors = new Action[]
        {
            static () => new BuildExecutionOutput(
                Verdict: AssuranceVerdict.Pass,
                Project: null!,
                Build: null!,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: []),
            static () => new CompileExecutionOutput(
                Verdict: AssuranceVerdict.Pass,
                Project: null!,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: [],
                RequestedMode: AssuranceRequestedExecutionMode.Auto,
                ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
                SessionKind: AssuranceSessionKind.TransientProbe,
                TimeoutMilliseconds: 1,
                Compile: null!),
            static () => new ReadyExecutionOutput(
                Verdict: AssuranceVerdict.Pass,
                Project: null!,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: [],
                Target: ReadyTarget.Execution,
                RequestedMode: AssuranceRequestedExecutionMode.Auto,
                ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
                SessionKind: AssuranceSessionKind.TransientProbe,
                TimeoutMilliseconds: 1,
                Lifecycle: null,
                ReadIndex: null),
            static () => new VerifyExecutionOutput(
                Verdict: AssuranceVerdict.Pass,
                Project: null!,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: [],
                Profile: null!,
                TimeoutMilliseconds: 1),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal(
                "Reports",
                Assert.Throws<ArgumentNullException>(constructor).ParamName));
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, AssuranceReportReference>> CreateReportSnapshots (
        IReadOnlyDictionary<string, AssuranceReportReference> reports)
    {
        var project = ProjectIdentityInfoTestFactory.Create();
        return
        [
            new CompileExecutionOutput(
                Verdict: AssuranceVerdict.Pass,
                Project: project,
                Verifiers: [],
                Claims: [],
                Reports: reports,
                ResidualRisks: [],
                RequestedMode: AssuranceRequestedExecutionMode.Auto,
                ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
                SessionKind: AssuranceSessionKind.TransientProbe,
                TimeoutMilliseconds: 1,
                Compile: AssuranceExecutionOutputTestFactory.CreateCompileOutput()).Reports,
            new ReadyExecutionOutput(
                Verdict: AssuranceVerdict.Pass,
                Project: project,
                Verifiers: [],
                Claims: [],
                Reports: reports,
                ResidualRisks: [],
                Target: ReadyTarget.Execution,
                RequestedMode: AssuranceRequestedExecutionMode.Auto,
                ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
                SessionKind: AssuranceSessionKind.TransientProbe,
                TimeoutMilliseconds: 1,
                Lifecycle: null,
                ReadIndex: null).Reports,
            new VerifyExecutionOutput(
                Verdict: AssuranceVerdict.Pass,
                Project: project,
                Verifiers: [],
                Claims: [],
                Reports: reports,
                ResidualRisks: [],
                Profile: AssuranceExecutionOutputTestFactory.CreateVerifyProfileOutput(),
                TimeoutMilliseconds: 1).Reports,
        ];
    }
}
