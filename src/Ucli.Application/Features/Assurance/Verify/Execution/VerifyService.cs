using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;

/// <summary> Executes verify assurance profiles and composes their verifier outputs. </summary>
internal sealed class VerifyService : IVerifyService
{
    private const string PostReadVerifierId = "postRead";
    private const string TestVerifierId = "test";
    private const string LogsVerifierId = "logs";
    private const string TestReportRef = "test.summary";
    private const string LogsReportRef = "logs.unity";

    private static readonly IReadOnlyList<VerifyResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<VerifyResidualRiskOutput>();

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IReadyService readyService;

    private readonly ICompileService compileService;

    private readonly ITestRunService testRunService;

    private readonly ILogsUnityService logsUnityService;

    private readonly IVerifyProfileFileReader profileFileReader;

    private readonly IVerifyFromInputFileReader fromInputFileReader;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="VerifyService" /> class. </summary>
    public VerifyService (
        IProjectContextResolver projectContextResolver,
        IReadyService readyService,
        ICompileService compileService,
        ITestRunService testRunService,
        ILogsUnityService logsUnityService,
        IVerifyProfileFileReader profileFileReader,
        IVerifyFromInputFileReader fromInputFileReader,
        TimeProvider? timeProvider = null)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.readyService = readyService ?? throw new ArgumentNullException(nameof(readyService));
        this.compileService = compileService ?? throw new ArgumentNullException(nameof(compileService));
        this.testRunService = testRunService ?? throw new ArgumentNullException(nameof(testRunService));
        this.logsUnityService = logsUnityService ?? throw new ArgumentNullException(nameof(logsUnityService));
        this.profileFileReader = profileFileReader ?? throw new ArgumentNullException(nameof(profileFileReader));
        this.fromInputFileReader = fromInputFileReader ?? throw new ArgumentNullException(nameof(fromInputFileReader));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<VerifyExecutionResult> ExecuteAsync (
        VerifyCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return VerifyExecutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Verify,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return VerifyExecutionResult.Failure(timeoutResult.Error!, project);
        }

        var timeout = timeoutResult.Timeout!.Value;
        var profileResult = await ResolveProfileAsync(
                input,
                context.UnityProject.RepositoryRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (!profileResult.IsSuccess)
        {
            return VerifyExecutionResult.Failure(profileResult.Error!, project);
        }

        VerifyFromInput? fromInput = null;
        if (!string.IsNullOrWhiteSpace(input.FromPath))
        {
            var fromFileResult = await fromInputFileReader.ReadAsync(
                input.FromPath!,
                context.UnityProject.RepositoryRoot,
                cancellationToken)
                .ConfigureAwait(false);
            if (!fromFileResult.IsSuccess)
            {
                return VerifyExecutionResult.Failure(fromFileResult.Error!, project);
            }

            var fromResult = VerifyFromInputReader.Read(
                fromFileResult.Json!,
                context.UnityProject.ProjectFingerprint);
            if (!fromResult.IsSuccess)
            {
                return VerifyExecutionResult.Failure(fromResult.Error!, project);
            }

            fromInput = fromResult.Input!;
        }

        var profile = profileResult.Profile!;
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var builder = new VerifyPacketBuilder(project);
        foreach (var step in profile.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldSkipConditionalStep(step, fromInput, builder))
            {
                continue;
            }

            if (!deadline.TryGetRemainingTimeout(out var stepTimeout))
            {
                return VerifyExecutionResult.Failure(
                    ApplicationFailure.Timeout("Timed out before verify profile step execution could begin."),
                    project);
            }

            VerifyStepExecutionResult stepResult;
            using (var stepCancellationScope = TimeProviderCancellationScope.CreateLinked(cancellationToken, stepTimeout, timeProvider))
            {
                try
                {
                    stepResult = await ExecuteStepAsync(
                            input,
                            stepTimeout,
                            step,
                            fromInput,
                            builder,
                            stepCancellationScope.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stepCancellationScope.HasTimedOut)
                {
                    return VerifyExecutionResult.Failure(
                        ApplicationFailure.Timeout("Timed out during verify profile step execution."),
                        project);
                }
            }

            if (!stepResult.IsSuccess)
            {
                return VerifyExecutionResult.Failure(stepResult.Error!, project);
            }

            if (deadline.IsExpired)
            {
                return VerifyExecutionResult.Failure(
                    ApplicationFailure.Timeout("Timed out during verify profile step execution."),
                    project);
            }
        }

        var profileDigest = VerifyProfileDigestCalculator.Calculate(profile);
        var output = new VerifyExecutionOutput(
            Verdict: RecalculateVerdict(builder.Claims, builder.ResidualRisks),
            Project: project,
            Verifiers: builder.Verifiers,
            Claims: builder.Claims,
            Reports: builder.Reports,
            ResidualRisks: builder.ResidualRisks,
            Profile: new VerifyProfileOutput(
                profile.Source,
                profile.Name,
                profile.RepositoryRelativePath,
                profileDigest),
            ProfileDigest: profileDigest,
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds));
        return VerifyExecutionResult.Success(output);
    }

    private async ValueTask<VerifyProfileResolutionResult> ResolveProfileAsync (
        VerifyCommandInput input,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(input.Profile) && !string.IsNullOrWhiteSpace(input.ProfilePath))
        {
            return VerifyProfileResolutionResult.Failure(ExecutionError.InvalidArgument(
                "--profile and --profilePath cannot be specified together."));
        }

        if (string.IsNullOrWhiteSpace(input.ProfilePath))
        {
            return VerifyProfileResolver.Resolve(input.Profile);
        }

        var readResult = await profileFileReader.ReadAsync(
                input.ProfilePath!,
                repositoryRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return VerifyProfileResolutionResult.Failure(readResult.Error!);
        }

        return VerifyProfileResolver.ResolveFileProfileJson(
            readResult.Json!,
            readResult.RepositoryRelativePath!);
    }

    private async ValueTask<VerifyStepExecutionResult> ExecuteStepAsync (
        VerifyCommandInput input,
        TimeSpan timeout,
        VerifyProfileStep step,
        VerifyFromInput? fromInput,
        VerifyPacketBuilder builder,
        CancellationToken cancellationToken)
    {
        return step.Kind switch
        {
            VerifyStepKindValues.Ready => await ExecuteReadyStepAsync(input, timeout, step, builder, cancellationToken).ConfigureAwait(false),
            VerifyStepKindValues.Compile => await ExecuteCompileStepAsync(input, timeout, step, builder, cancellationToken).ConfigureAwait(false),
            VerifyStepKindValues.PostRead => ExecutePostReadStep(step, fromInput, builder),
            VerifyStepKindValues.Test => await ExecuteTestStepAsync(input, timeout, step, builder, cancellationToken).ConfigureAwait(false),
            VerifyStepKindValues.Logs => await ExecuteLogsStepAsync(input, step, builder, cancellationToken).ConfigureAwait(false),
            _ => VerifyStepExecutionResult.Failure(ApplicationFailure.InvalidInput(
                $"Unsupported verify step kind '{step.Kind}'.",
                UcliCoreErrorCodes.InvalidArgument)),
        };
    }

    private async ValueTask<VerifyStepExecutionResult> ExecuteReadyStepAsync (
        VerifyCommandInput input,
        TimeSpan timeout,
        VerifyProfileStep step,
        VerifyPacketBuilder builder,
        CancellationToken cancellationToken)
    {
        var result = await readyService.ExecuteAsync(
                new ReadyCommandInput(
                    ProjectPath: input.ProjectPath,
                    Target: step.ReadyTarget,
                    Mode: input.Mode,
                    TimeoutMilliseconds: ToTimeoutMilliseconds(timeout),
                    ReadIndexMode: null,
                    IsReadIndexModeSpecified: false,
                    FailFast: false),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return VerifyStepExecutionResult.Failure(result.Errors[0]);
        }

        var output = result.Output!;
        for (var i = 0; i < output.Verifiers.Count; i++)
        {
            var verifier = output.Verifiers[i];
            builder.AddVerifier(new VerifyVerifierOutput(
                Id: verifier.Id,
                Kind: VerifyStepKindValues.Ready,
                Deterministic: verifier.Deterministic,
                Required: step.Required,
                PrimaryClaims: verifier.PrimaryClaims,
                Effects: step.Effects));
        }

        foreach (var claim in output.Claims)
        {
            builder.AddClaim(new VerifyClaimOutput(
                Id: claim.Id,
                Status: claim.Status,
                Coverage: claim.Coverage,
                Required: step.Required && claim.Required,
                VerifierRef: claim.VerifierRef,
                Statement: claim.Statement,
                Subject: claim.Subject,
                Evidence: claim.Evidence.Select(static evidence => new VerifyEvidenceOutput(evidence.Kind)
                {
                    EvidenceRef = evidence.EvidenceRef,
                    Data = evidence.Data,
                }).ToArray(),
                ResidualRisks: claim.ResidualRisks.Select(static risk => new VerifyResidualRiskOutput(risk.Code, risk.Blocking)
                {
                    Message = risk.Message,
                }).ToArray())
            {
                Validity = claim.Validity,
            });
        }

        foreach (var report in output.Reports)
        {
            builder.AddReport(report.Key, new VerifyReportOutput(report.Value.Kind)
            {
                Path = report.Value.Path,
                Uri = report.Value.Uri,
                Digest = report.Value.Digest,
            });
        }

        return VerifyStepExecutionResult.Success();
    }

    private async ValueTask<VerifyStepExecutionResult> ExecuteCompileStepAsync (
        VerifyCommandInput input,
        TimeSpan timeout,
        VerifyProfileStep step,
        VerifyPacketBuilder builder,
        CancellationToken cancellationToken)
    {
        var result = await compileService.ExecuteAsync(
                new CompileCommandInput(
                    ProjectPath: input.ProjectPath,
                    Mode: input.Mode,
                    TimeoutMilliseconds: ToTimeoutMilliseconds(timeout)),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return VerifyStepExecutionResult.Failure(result.Errors[0]);
        }

        var output = result.Output!;
        foreach (var verifier in output.Verifiers)
        {
            builder.AddVerifier(new VerifyVerifierOutput(
                Id: verifier.Id,
                Kind: VerifyStepKindValues.Compile,
                Deterministic: verifier.Deterministic,
                Required: step.Required,
                PrimaryClaims: verifier.PrimaryClaims,
                Effects: step.Effects)
            {
                ReportRef = verifier.ReportRef,
            });
        }

        foreach (var claim in output.Claims)
        {
            builder.AddClaim(ProjectCompileClaim(claim, step.Required));
        }

        foreach (var report in output.Reports)
        {
            builder.AddReport(report.Key, new VerifyReportOutput(report.Value.Kind)
            {
                Path = report.Value.Path,
                Digest = report.Value.Digest,
            });
        }

        return VerifyStepExecutionResult.Success();
    }

    private VerifyStepExecutionResult ExecutePostReadStep (
        VerifyProfileStep step,
        VerifyFromInput? fromInput,
        VerifyPacketBuilder builder)
    {
        if (fromInput is null)
        {
            if (!step.Required)
            {
                return VerifyStepExecutionResult.Success();
            }

            var claim = CreatePostReadClaim(
                VerifyClaimCodes.ReadSurfaceSafe.Value,
                VerifyClaimStatusValues.Unverified,
                VerifyCoverageValues.None,
                required: true,
                "No --from input was provided for required post-read verification.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                },
                []);
            builder.AddVerifier(new VerifyVerifierOutput(
                PostReadVerifierId,
                VerifyStepKindValues.PostRead,
                Deterministic: true,
                Required: true,
                PrimaryClaims: [claim.Id],
                Effects: step.Effects));
            builder.AddClaim(claim);
            return VerifyStepExecutionResult.Success();
        }

        var claimSet = CreatePostReadClaims(fromInput, step.Required);
        foreach (var residualRisk in claimSet.ResidualRisks)
        {
            builder.AddResidualRisk(residualRisk);
        }

        if (claimSet.Claims.Count == 0)
        {
            return VerifyStepExecutionResult.Success();
        }

        builder.AddVerifier(new VerifyVerifierOutput(
            PostReadVerifierId,
            VerifyStepKindValues.PostRead,
            Deterministic: true,
            Required: claimSet.Claims.Any(static claim => claim.Required),
            PrimaryClaims: claimSet.Claims.Select(static claim => claim.Id).ToArray(),
            Effects: step.Effects));
        foreach (var claim in claimSet.Claims)
        {
            builder.AddClaim(claim);
        }

        return VerifyStepExecutionResult.Success();
    }

    private async ValueTask<VerifyStepExecutionResult> ExecuteTestStepAsync (
        VerifyCommandInput input,
        TimeSpan timeout,
        VerifyProfileStep step,
        VerifyPacketBuilder builder,
        CancellationToken cancellationToken)
    {
        var result = await testRunService.ExecuteAsync(
                new TestRunCommandInput(
                    ProjectPath: input.ProjectPath,
                    ProfilePath: null,
                    Mode: input.Mode,
                    UnityVersion: null,
                    UnityEditorPath: null,
                    TestPlatform: step.TestPlatform,
                    TestFilter: step.TestFilter,
                    TestCategory: step.TestCategory,
                    AssemblyName: step.AssemblyName,
                    TestSettingsPath: null,
                    TimeoutMilliseconds: ToTimeoutMilliseconds(timeout),
                    FailFast: false),
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ErrorKind is not null)
        {
            return VerifyStepExecutionResult.Failure(result.Failure!);
        }

        var status = result.Result == TestRunResultKind.Pass
            ? VerifyClaimStatusValues.Passed
            : VerifyClaimStatusValues.Failed;
        var reportRef = string.IsNullOrWhiteSpace(result.SummaryJsonPath) ? null : TestReportRef;
        builder.AddVerifier(new VerifyVerifierOutput(
            TestVerifierId,
            VerifyStepKindValues.Test,
            Deterministic: false,
            Required: step.Required,
            PrimaryClaims: [VerifyClaimCodes.UnityTestsPassed.Value],
            Effects: step.Effects)
        {
            ReportRef = reportRef,
        });
        if (reportRef != null)
        {
            builder.AddReport(reportRef, new VerifyReportOutput("test.summary")
            {
                Path = result.SummaryJsonPath,
            });
        }

        builder.AddClaim(new VerifyClaimOutput(
            Id: VerifyClaimCodes.UnityTestsPassed.Value,
            Status: status,
            Coverage: VerifyCoverageValues.Full,
            Required: step.Required,
            VerifierRef: TestVerifierId,
            Statement: status == VerifyClaimStatusValues.Passed
                ? "Unity Test Runner execution passed."
                : "Unity Test Runner execution did not pass.",
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "unityTests",
                ["runId"] = result.RunId,
            },
            Evidence: reportRef is null
                ? []
                :
                [
                    new VerifyEvidenceOutput("testSummary")
                    {
                        EvidenceRef = reportRef,
                    },
                ],
            ResidualRisks: EmptyResidualRisks));
        return VerifyStepExecutionResult.Success();
    }

    private async ValueTask<VerifyStepExecutionResult> ExecuteLogsStepAsync (
        VerifyCommandInput input,
        VerifyProfileStep step,
        VerifyPacketBuilder builder,
        CancellationToken cancellationToken)
    {
        if (!builder.HasNonPassingClaim)
        {
            return VerifyStepExecutionResult.Success();
        }

        var eventCount = 0;
        var result = await logsUnityService.ExecuteAsync(
                new LogsUnityServiceRequest(
                    ProjectPath: input.ProjectPath,
                    Tail: 200,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: "all",
                    Query: null,
                    QueryTarget: null,
                    Source: "all",
                    StackTrace: "error",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: 8000,
                    Stream: false,
                    PollIntervalMilliseconds: null,
                    IdleTimeoutMilliseconds: null),
                (logEvent, _, _) =>
                {
                    eventCount++;
                    return ValueTask.CompletedTask;
                },
                cancellationToken)
            .ConfigureAwait(false);

        var reportUri = result.IsSuccess
            ? $"ucli://logs/unity?tail=200&count={eventCount}"
            : $"ucli://logs/unity?tail=200&status=failed";
        builder.AddReport(LogsReportRef, new VerifyReportOutput("unityLog")
        {
            Uri = reportUri,
        });
        builder.AddVerifier(new VerifyVerifierOutput(
            LogsVerifierId,
            VerifyStepKindValues.Logs,
            Deterministic: false,
            Required: false,
            PrimaryClaims: [],
            Effects: step.Effects)
        {
            ReportRef = LogsReportRef,
        });
        return VerifyStepExecutionResult.Success();
    }

    private static bool ShouldSkipConditionalStep (
        VerifyProfileStep step,
        VerifyFromInput? fromInput,
        VerifyPacketBuilder builder)
    {
        if (string.Equals(step.Kind, VerifyStepKindValues.PostRead, StringComparison.Ordinal)
            && !step.Required
            && (fromInput is null || !fromInput.NeedsPostRead))
        {
            return true;
        }

        return string.Equals(step.Kind, VerifyStepKindValues.Logs, StringComparison.Ordinal)
            && !builder.HasNonPassingClaim;
    }

    private static PostReadClaimSet CreatePostReadClaims (
        VerifyFromInput fromInput,
        bool profileRequired)
    {
        var claims = new List<VerifyClaimOutput>();
        var residualRisks = new List<VerifyResidualRiskOutput>();
        var diagnostics = fromInput.OpResults.SelectMany(static result => result.Diagnostics).ToArray();
        var neutralEvidence = CreatePostReadEvidence(fromInput, diagnostics.Length, IpcExecuteDiagnosticCoverageImpactNames.None);

        var persistenceResults = fromInput.OpResults
            .Where(static result => result.Changed
                && result.PostReadSource.PersistenceExpected
                && !result.PostReadSource.PlayModeMutation)
            .ToArray();
        if (persistenceResults.Length != 0)
        {
            var persistenceDiagnostics = SelectDiagnostics(persistenceResults);
            var persistenceStatus = ResolveDiagnosticStatus(persistenceDiagnostics);
            var persistenceCoverage = ResolveDiagnosticCoverage(persistenceDiagnostics);
            var persistenceEvidence = CreatePostReadEvidence(fromInput, persistenceDiagnostics.Count, ResolveDiagnosticImpact(persistenceDiagnostics));
            var touchedCount = persistenceResults.Sum(static result => result.TouchedCount);
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PersistenceUnitTouched.Value,
                touchedCount > 0 || string.Equals(persistenceStatus, VerifyClaimStatusValues.Failed, StringComparison.Ordinal)
                    ? persistenceStatus
                    : VerifyClaimStatusValues.Indeterminate,
                touchedCount > 0 ? persistenceCoverage : VerifyCoverageValues.None,
                required: profileRequired,
                touchedCount > 0
                    ? "Touched persistence units were observed from the input result."
                    : "Changed operations did not report touched persistence units.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["changedCount"] = persistenceResults.Length,
                    ["touchedCount"] = touchedCount,
                },
                persistenceEvidence));
        }

        var hasReadSurfaceClaim = fromInput.ReadPostconditionRequirementCount > 0;
        if (hasReadSurfaceClaim)
        {
            var readDiagnostics = diagnostics;
            var readStatus = ResolveDiagnosticStatus(readDiagnostics);
            var readCoverage = ResolveDiagnosticCoverage(readDiagnostics);
            var readEvidence = CreatePostReadEvidence(fromInput, readDiagnostics.Length, ResolveDiagnosticImpact(readDiagnostics));
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.ReadSurfaceSafe.Value,
                readStatus,
                readCoverage,
                required: profileRequired,
                "Read-postcondition requirements were observed for affected read surfaces.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["requirementCount"] = fromInput.ReadPostconditionRequirementCount,
                },
                readEvidence));
        }

        var deterministicMutationResults = fromInput.OpResults
            .Where(static result =>
                (result.Applied || result.Changed)
                && IpcExecutePostReadSourceRules.IsDeterministicMutationSource(
                    result.PostReadSource.SourceKind,
                    result.PostReadSource.ExpectedPostState))
            .ToArray();
        var deterministicMutationCount = deterministicMutationResults.Length;
        if (deterministicMutationCount > 0)
        {
            var deterministicDiagnostics = SelectDiagnostics(deterministicMutationResults);
            var deterministicStatus = ResolveDiagnosticStatus(deterministicDiagnostics);
            var deterministicCoverage = ResolveDiagnosticCoverage(deterministicDiagnostics);
            var deterministicEvidence = CreatePostReadEvidence(fromInput, deterministicDiagnostics.Count, ResolveDiagnosticImpact(deterministicDiagnostics));
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PostMutationObserved.Value,
                deterministicStatus,
                deterministicCoverage,
                required: profileRequired,
                "Deterministic post-mutation state was observed from the input result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["observedMutationCount"] = deterministicMutationCount,
                },
                deterministicEvidence));
        }
        else if (fromInput.OpResults.Any(static result => result.Applied || result.Changed))
        {
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PostMutationObserved.Value,
                VerifyClaimStatusValues.OutOfScope,
                VerifyCoverageValues.None,
                required: false,
                "Expected post-mutation state is not deterministic from the input result alone.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["reason"] = "expectedPostStateUnavailable",
                },
                neutralEvidence));
        }

        if (HasUnboundDiagnosticImpact(fromInput.OpResults, persistenceResults, deterministicMutationResults, hasReadSurfaceClaim))
        {
            residualRisks.Add(new VerifyResidualRiskOutput(
                VerifyRiskCodes.FromDiagnosticCoverageUnbound.Value,
                Blocking: true)
            {
                Message = "Input diagnostics affected coverage but no generated post-read claim could carry that diagnostic impact.",
            });
        }

        if (claims.Count == 0 && profileRequired)
        {
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PostMutationObserved.Value,
                VerifyClaimStatusValues.Unverified,
                VerifyCoverageValues.None,
                required: true,
                "No mutation completion evidence was available in the input result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["reason"] = "noMutationEvidence",
                },
                neutralEvidence));
        }

        return new PostReadClaimSet(claims, residualRisks);
    }

    private static IReadOnlyList<VerifyFromDiagnostic> SelectDiagnostics (IReadOnlyList<VerifyFromOperationResult> results)
    {
        return results.SelectMany(static result => result.Diagnostics).ToArray();
    }

    private static bool HasUnboundDiagnosticImpact (
        IReadOnlyList<VerifyFromOperationResult> opResults,
        IReadOnlyList<VerifyFromOperationResult> persistenceResults,
        IReadOnlyList<VerifyFromOperationResult> deterministicMutationResults,
        bool hasReadSurfaceClaim)
    {
        if (hasReadSurfaceClaim)
        {
            return false;
        }

        var boundOpIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var result in persistenceResults)
        {
            boundOpIds.Add(result.OpId);
        }

        foreach (var result in deterministicMutationResults)
        {
            boundOpIds.Add(result.OpId);
        }

        return opResults.Any(result =>
            !boundOpIds.Contains(result.OpId)
            && !string.Equals(ResolveDiagnosticImpact(result.Diagnostics), IpcExecuteDiagnosticCoverageImpactNames.None, StringComparison.Ordinal));
    }

    private static VerifyEvidenceOutput[] CreatePostReadEvidence (
        VerifyFromInput fromInput,
        int diagnosticCount,
        string diagnosticImpact)
    {
        return
        [
            new VerifyEvidenceOutput("fromResultSummary")
            {
                Data = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["command"] = fromInput.Command,
                    ["opResultCount"] = fromInput.OpResults.Count,
                    ["changedCount"] = fromInput.OpResults.Count(static result => result.Changed),
                    ["touchedCount"] = fromInput.OpResults.Sum(static result => result.TouchedCount),
                    ["diagnosticCount"] = diagnosticCount,
                    ["diagnosticImpact"] = diagnosticImpact,
                },
            },
        ];
    }

    private static VerifyClaimOutput CreatePostReadClaim (
        string id,
        string status,
        string coverage,
        bool required,
        string statement,
        IReadOnlyDictionary<string, object?> subject,
        IReadOnlyList<VerifyEvidenceOutput> evidence)
    {
        return new VerifyClaimOutput(
            Id: id,
            Status: status,
            Coverage: coverage,
            Required: required,
            VerifierRef: PostReadVerifierId,
            Statement: statement,
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static string ResolveDiagnosticStatus (IReadOnlyList<VerifyFromDiagnostic> diagnostics)
    {
        if (diagnostics.Any(static diagnostic => string.Equals(
                diagnostic.Severity,
                IpcExecuteDiagnosticSeverityNames.Error,
                StringComparison.Ordinal)))
        {
            return VerifyClaimStatusValues.Failed;
        }

        return diagnostics.Any(static diagnostic => string.Equals(
                diagnostic.CoverageImpact,
                IpcExecuteDiagnosticCoverageImpactNames.Indeterminate,
                StringComparison.Ordinal))
            ? VerifyClaimStatusValues.Indeterminate
            : VerifyClaimStatusValues.Passed;
    }

    private static string ResolveDiagnosticCoverage (IReadOnlyList<VerifyFromDiagnostic> diagnostics)
    {
        if (diagnostics.Any(static diagnostic => string.Equals(
                diagnostic.CoverageImpact,
                IpcExecuteDiagnosticCoverageImpactNames.Indeterminate,
                StringComparison.Ordinal)))
        {
            return VerifyCoverageValues.None;
        }

        return diagnostics.Any(static diagnostic => string.Equals(
                diagnostic.CoverageImpact,
                IpcExecuteDiagnosticCoverageImpactNames.Partial,
                StringComparison.Ordinal))
            ? VerifyCoverageValues.Partial
            : VerifyCoverageValues.Full;
    }

    private static string ResolveDiagnosticImpact (IReadOnlyList<VerifyFromDiagnostic> diagnostics)
    {
        if (diagnostics.Any(static diagnostic => string.Equals(
                diagnostic.Severity,
                IpcExecuteDiagnosticSeverityNames.Error,
                StringComparison.Ordinal)))
        {
            return IpcExecuteDiagnosticSeverityNames.Error;
        }

        if (diagnostics.Any(static diagnostic => string.Equals(
                diagnostic.CoverageImpact,
                IpcExecuteDiagnosticCoverageImpactNames.Indeterminate,
                StringComparison.Ordinal)))
        {
            return IpcExecuteDiagnosticCoverageImpactNames.Indeterminate;
        }

        return diagnostics.Any(static diagnostic => string.Equals(
                diagnostic.CoverageImpact,
                IpcExecuteDiagnosticCoverageImpactNames.Partial,
                StringComparison.Ordinal))
            ? IpcExecuteDiagnosticCoverageImpactNames.Partial
            : IpcExecuteDiagnosticCoverageImpactNames.None;
    }

    private static VerifyClaimOutput ProjectCompileClaim (
        CompileClaimOutput claim,
        bool required)
    {
        return new VerifyClaimOutput(
            Id: claim.Id,
            Status: claim.Status,
            Coverage: claim.Coverage,
            Required: required && claim.Required,
            VerifierRef: claim.VerifierRef,
            Statement: claim.Statement,
            Subject: claim.Subject,
            Evidence: claim.Evidence.Select(static evidence => new VerifyEvidenceOutput(evidence.Kind)
            {
                EvidenceRef = evidence.EvidenceRef,
                Data = evidence.Data,
            }).ToArray(),
            ResidualRisks: claim.ResidualRisks.Select(static risk => new VerifyResidualRiskOutput(risk.Code, risk.Blocking)).ToArray());
    }

    private static int ToTimeoutMilliseconds (TimeSpan timeout)
    {
        var timeoutMilliseconds = Math.Ceiling(timeout.TotalMilliseconds);
        if (timeoutMilliseconds >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return checked((int)timeoutMilliseconds);
    }

    private static string RecalculateVerdict (
        IReadOnlyList<VerifyClaimOutput> claims,
        IReadOnlyList<VerifyResidualRiskOutput> payloadResidualRisks)
    {
        if (payloadResidualRisks.Any(static risk => risk.Blocking)
            || claims.Any(static claim => claim.ResidualRisks.Any(static risk => risk.Blocking)))
        {
            return VerifyVerdictValues.Fail;
        }

        var requiredClaims = claims.Where(static claim => claim.Required).ToArray();
        if (requiredClaims.Any(static claim => string.Equals(claim.Status, VerifyClaimStatusValues.Failed, StringComparison.Ordinal)))
        {
            return VerifyVerdictValues.Fail;
        }

        if (requiredClaims.Any(static claim =>
                !string.Equals(claim.Status, VerifyClaimStatusValues.Passed, StringComparison.Ordinal)
                || !string.Equals(claim.Coverage, VerifyCoverageValues.Full, StringComparison.Ordinal)))
        {
            return VerifyVerdictValues.Incomplete;
        }

        return VerifyVerdictValues.Pass;
    }

    private sealed class VerifyPacketBuilder
    {
        private readonly Dictionary<string, VerifyReportOutput> reports = new(StringComparer.Ordinal);
        private readonly List<VerifyVerifierOutput> verifiers = [];
        private readonly List<VerifyClaimOutput> claims = [];
        private readonly List<VerifyResidualRiskOutput> residualRisks = [];

        public VerifyPacketBuilder (ProjectIdentityInfo project)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public ProjectIdentityInfo Project { get; }

        public IReadOnlyList<VerifyVerifierOutput> Verifiers => verifiers;

        public IReadOnlyList<VerifyClaimOutput> Claims => claims;

        public IReadOnlyDictionary<string, VerifyReportOutput> Reports => reports;

        public IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks => residualRisks;

        public bool HasNonPassingClaim => claims.Any(static claim =>
            string.Equals(claim.Status, VerifyClaimStatusValues.Failed, StringComparison.Ordinal)
            || string.Equals(claim.Status, VerifyClaimStatusValues.Indeterminate, StringComparison.Ordinal)
            || string.Equals(claim.Status, VerifyClaimStatusValues.Unverified, StringComparison.Ordinal)
            || (claim.Required
                && !string.Equals(claim.Coverage, VerifyCoverageValues.Full, StringComparison.Ordinal)));

        public void AddVerifier (VerifyVerifierOutput verifier)
        {
            ArgumentNullException.ThrowIfNull(verifier);
            verifiers.Add(verifier);
        }

        public void AddClaim (VerifyClaimOutput claim)
        {
            ArgumentNullException.ThrowIfNull(claim);
            claims.Add(claim);
        }

        public void AddResidualRisk (VerifyResidualRiskOutput residualRisk)
        {
            ArgumentNullException.ThrowIfNull(residualRisk);
            residualRisks.Add(residualRisk);
        }

        public void AddReport (
            string key,
            VerifyReportOutput report)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(report);
            reports[key] = report;
        }
    }

    private sealed record VerifyStepExecutionResult (ApplicationFailure? Error)
    {
        public bool IsSuccess => Error is null;

        public static VerifyStepExecutionResult Success ()
        {
            return new VerifyStepExecutionResult((ApplicationFailure?)null);
        }

        public static VerifyStepExecutionResult Failure (ApplicationFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new VerifyStepExecutionResult(failure);
        }
    }

    private sealed record PostReadClaimSet (
        IReadOnlyList<VerifyClaimOutput> Claims,
        IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks);
}
