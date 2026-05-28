using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Execution.PostRead;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;

/// <summary> Executes verify assurance profiles and composes their verifier outputs. </summary>
internal sealed class VerifyService : IVerifyService
{
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
        ICommandProgressSink? progressSink = null,
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
        var profileDigest = VerifyProfileDigestCalculator.Calculate(profile);
        await EmitProgressEntryAsync(
                progressSink,
                VerifyProgressEventNames.Started,
                CreateProgressEntry(profile, profileDigest, verdict: null),
                cancellationToken)
            .ConfigureAwait(false);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var builder = new VerifyPacketBuilder();
        foreach (var step in profile.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!VerifyStepKindValues.IsSupported(step.Kind))
            {
                var failure = ApplicationFailure.InvalidInput(
                    $"Unsupported verify step kind '{step.Kind}'.",
                    UcliCoreErrorCodes.InvalidArgument);
                await EmitDiagnosticAsync(progressSink, failure, stepKind: null, cancellationToken).ConfigureAwait(false);
                return VerifyExecutionResult.Failure(failure, project);
            }

            if (TryGetConditionalSkipReason(step, fromInput, builder, out var skipReason))
            {
                await EmitProgressEntryAsync(
                        progressSink,
                        VerifyProgressEventNames.StepSkipped,
                        CreateStepProgressEntry(step, skipReason),
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (!deadline.TryGetRemainingTimeout(out var stepTimeout))
            {
                var failure = ApplicationFailure.Timeout("Timed out before verify profile step execution could begin.");
                await EmitDiagnosticAsync(progressSink, failure, step.Kind, cancellationToken).ConfigureAwait(false);
                return VerifyExecutionResult.Failure(failure, project);
            }

            await EmitProgressEntryAsync(
                    progressSink,
                    VerifyProgressEventNames.StepStarted,
                    CreateStepProgressEntry(step, skipReason: null),
                    cancellationToken)
                .ConfigureAwait(false);

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
                    var failure = ApplicationFailure.Timeout("Timed out during verify profile step execution.");
                    await EmitDiagnosticAsync(progressSink, failure, step.Kind, cancellationToken).ConfigureAwait(false);
                    return VerifyExecutionResult.Failure(failure, project);
                }
            }

            if (!stepResult.IsSuccess)
            {
                await EmitDiagnosticAsync(progressSink, stepResult.Error!, step.Kind, cancellationToken).ConfigureAwait(false);
                return VerifyExecutionResult.Failure(stepResult.Error!, project);
            }

            await EmitProgressEntryAsync(
                    progressSink,
                    VerifyProgressEventNames.StepCompleted,
                    CreateStepProgressEntry(step, skipReason: null),
                    cancellationToken)
                .ConfigureAwait(false);

            if (deadline.IsExpired)
            {
                var failure = ApplicationFailure.Timeout("Timed out during verify profile step execution.");
                await EmitDiagnosticAsync(progressSink, failure, step.Kind, cancellationToken).ConfigureAwait(false);
                return VerifyExecutionResult.Failure(failure, project);
            }
        }

        var output = new VerifyExecutionOutput(
            Verdict: VerifyVerdictCalculator.Calculate(builder.Claims, builder.ResidualRisks),
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
        await EmitProgressEntryAsync(
                progressSink,
                VerifyProgressEventNames.Completed,
                CreateProgressEntry(profile, profileDigest, output.Verdict),
                cancellationToken)
            .ConfigureAwait(false);
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

            var missingClaimSet = PostReadClaimBuilder.BuildMissingInput();
            AddPostReadClaimSet(step, missingClaimSet, builder);
            return VerifyStepExecutionResult.Success();
        }

        var claimSet = PostReadClaimBuilder.Build(fromInput, step.Required);
        AddPostReadClaimSet(step, claimSet, builder);
        return VerifyStepExecutionResult.Success();
    }

    private static void AddPostReadClaimSet (
        VerifyProfileStep step,
        PostReadClaimSet claimSet,
        VerifyPacketBuilder builder)
    {
        foreach (var residualRisk in claimSet.ResidualRisks)
        {
            builder.AddResidualRisk(residualRisk);
        }

        if (claimSet.Claims.Count == 0)
        {
            return;
        }

        builder.AddVerifier(new VerifyVerifierOutput(
            PostReadClaimBuilder.VerifierId,
            VerifyStepKindValues.PostRead,
            Deterministic: true,
            Required: claimSet.Claims.Any(static claim => claim.Required),
            PrimaryClaims: claimSet.Claims.Select(static claim => claim.Id).ToArray(),
            Effects: step.Effects));
        foreach (var claim in claimSet.Claims)
        {
            builder.AddClaim(claim);
        }
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
                cancellationToken: cancellationToken)
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

    private static bool TryGetConditionalSkipReason (
        VerifyProfileStep step,
        VerifyFromInput? fromInput,
        VerifyPacketBuilder builder,
        out string skipReason)
    {
        if (string.Equals(step.Kind, VerifyStepKindValues.PostRead, StringComparison.Ordinal)
            && !step.Required
            && (fromInput is null || !fromInput.NeedsPostRead))
        {
            skipReason = VerifyStepSkipReasons.PostReadNotNeeded;
            return true;
        }

        if (string.Equals(step.Kind, VerifyStepKindValues.Logs, StringComparison.Ordinal)
            && !builder.HasNonPassingClaim)
        {
            skipReason = VerifyStepSkipReasons.LogsNotNeeded;
            return true;
        }

        skipReason = string.Empty;
        return false;
    }

    private static VerifyProgressEntry CreateProgressEntry (
        VerifyProfileDefinition profile,
        string profileDigest,
        string? verdict)
    {
        return new VerifyProgressEntry(
            profile.Source,
            profile.Name,
            profile.RepositoryRelativePath,
            profileDigest,
            profile.Steps.Count,
            verdict);
    }

    private static VerifyStepProgressEntry CreateStepProgressEntry (
        VerifyProfileStep step,
        string? skipReason)
    {
        return new VerifyStepProgressEntry(
            step.Kind,
            step.Required,
            step.Effects.ToArray(),
            skipReason);
    }

    private static ValueTask EmitDiagnosticAsync (
        ICommandProgressSink? progressSink,
        ApplicationFailure failure,
        string? stepKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return EmitProgressEntryAsync(
            progressSink,
            VerifyProgressEventNames.Diagnostic,
            new VerifyDiagnosticEntry(
                failure.Code.Value,
                failure.Message,
                "error",
                VerifyStepKindValues.IsSupported(stepKind ?? string.Empty) ? stepKind : null),
            cancellationToken);
    }

    private static ValueTask EmitProgressEntryAsync (
        ICommandProgressSink? progressSink,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (progressSink is null)
        {
            return ValueTask.CompletedTask;
        }

        return progressSink.OnEntryAsync(eventName, payload, cancellationToken);
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
}
