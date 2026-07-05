using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.VerifyServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyServiceTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithBuiltInMutationProfile_DoesNotExecuteCompile ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithBuiltInMutationProfile_DoesNotExecuteCompile));
        var compileService = new RecordingVerifyCompileService(_ => throw new InvalidOperationException("Compile must not execute."));
        var service = CreateService(scope.FullPath, compileService: compileService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:mutation",
            ProfilePath: null,
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        VerifyServiceAssert.BuiltInMutationProfileCompletedWithoutCompile(
            result,
            compileService);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithProgressSink_DoesNotChangeFinalOutput ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithProgressSink_DoesNotChangeFinalOutput));
        var input = new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:default",
            ProfilePath: null,
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000);
        var serviceWithoutProgress = CreateService(scope.FullPath);
        var withoutProgress = await serviceWithoutProgress.ExecuteAsync(input);
        var progressSink = new CollectingCommandProgressSink();
        var serviceWithProgress = CreateService(scope.FullPath);

        var withProgress = await serviceWithProgress.ExecuteAsync(input, progressSink);

        Assert.True(withoutProgress.IsSuccess);
        Assert.True(withProgress.IsSuccess);
        Assert.Equal(
            JsonSerializer.Serialize(withoutProgress.Output),
            JsonSerializer.Serialize(withProgress.Output));
        Assert.NotEmpty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithConditionalStepsSkipped_EmitsSkipEntriesWithoutFinalVerifiers ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithConditionalStepsSkipped_EmitsSkipEntriesWithoutFinalVerifiers));
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(
            new VerifyCommandInput(
                ProjectPath: null,
                Profile: "built-in:default",
                ProfilePath: null,
                FromPath: null,
                Mode: UnityExecutionMode.Auto,
                TimeoutMilliseconds: 10000),
            progressSink);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Output!.Verifiers, static verifier => string.Equals(verifier.Kind, VerifyStepKindValues.PostRead, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Output.Verifiers, static verifier => string.Equals(verifier.Kind, VerifyStepKindValues.Logs, StringComparison.Ordinal));
        var skippedEntries = progressSink.Entries
            .Where(static entry => string.Equals(entry.EventName, VerifyProgressEventNames.StepSkipped, StringComparison.Ordinal))
            .Select(static entry => Assert.IsType<VerifyStepProgressEntry>(entry.Payload))
            .ToArray();
        Assert.Contains(skippedEntries, static entry =>
            string.Equals(entry.Kind, VerifyStepKindValues.PostRead, StringComparison.Ordinal)
            && string.Equals(entry.SkipReason, VerifyStepSkipReasons.PostReadNotNeeded, StringComparison.Ordinal));
        Assert.Contains(skippedEntries, static entry =>
            string.Equals(entry.Kind, VerifyStepKindValues.Logs, StringComparison.Ordinal)
            && string.Equals(entry.SkipReason, VerifyStepSkipReasons.LogsNotNeeded, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenStepFails_EmitsDiagnosticWithoutVerifyCompleted ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WhenStepFails_EmitsDiagnosticWithoutVerifyCompleted));
        var progressSink = new CollectingCommandProgressSink();
        var compileService = new RecordingVerifyCompileService(_ => CompileExecutionResult.Failure(
            ExecutionError.InternalError("Compile command failed.")));
        var service = CreateService(scope.FullPath, compileService: compileService);

        var result = await service.ExecuteAsync(
            new VerifyCommandInput(
                ProjectPath: null,
                Profile: "built-in:script",
                ProfilePath: null,
                FromPath: null,
                Mode: UnityExecutionMode.Auto,
                TimeoutMilliseconds: 10000),
            progressSink);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(progressSink.Entries, static entry => string.Equals(entry.EventName, VerifyProgressEventNames.Completed, StringComparison.Ordinal));
        var diagnosticEntry = Assert.Single(progressSink.Entries, static entry => string.Equals(entry.EventName, VerifyProgressEventNames.Diagnostic, StringComparison.Ordinal));
        var diagnostic = Assert.IsType<VerifyDiagnosticEntry>(diagnosticEntry.Payload);
        Assert.Equal(VerifyStepKindValues.Compile, diagnostic.StepKind);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Equal("Compile command failed.", diagnostic.Message);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnsupportedProfileStep_DoesNotEmitUnknownStepEntry ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithUnsupportedProfileStep_DoesNotEmitUnknownStepEntry));
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "steps": [
                {
                  "kind": "external",
                  "required": true
                }
              ]
            }
            """);
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(
            new VerifyCommandInput(
                ProjectPath: null,
                Profile: null,
                ProfilePath: "verify.json",
                FromPath: null,
                Mode: UnityExecutionMode.Auto,
                TimeoutMilliseconds: 10000),
            progressSink);

        Assert.False(result.IsSuccess);
        Assert.Empty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithProfileAndProfilePath_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithProfileAndProfilePath_ReturnsInvalidArgument));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:default",
            ProfilePath: "verify.json",
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithFromFingerprintMismatch_ReturnsProjectFingerprintMismatch ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithFromFingerprintMismatch_ReturnsProjectFingerprintMismatch));
        var fromPath = scope.WriteFile("from.json", CreateFromJson("other-fingerprint", coverageImpact: "none"));
        var readyService = new RecordingVerifyReadyService(input => CreateReadyResult(input.Target, ProjectIdentityInfoTestFactory.CreateForRepositoryRoot(scope.FullPath)));
        var service = CreateService(scope.FullPath, readyService: readyService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:mutation",
            ProfilePath: null,
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        VerifyServiceAssert.ProjectFingerprintMismatchStoppedBeforeReady(
            result,
            readyService);
    }


    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenReadyConsumesTimeoutBudget_PassesRemainingTimeoutToCompile ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WhenReadyConsumesTimeoutBudget_PassesRemainingTimeoutToCompile));
        var timeProvider = new ManualTimeProvider();
        var project = ProjectIdentityInfoTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var readyService = new RecordingVerifyReadyService(input =>
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            return CreateReadyResult(input.Target, project);
        });
        var compileService = new RecordingVerifyCompileService(_ => CreateCompileResult(project));
        var service = CreateService(
            scope.FullPath,
            readyService: readyService,
            compileService: compileService,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:default",
            ProfilePath: null,
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 1000));

        Assert.True(result.IsSuccess);
        VerifyStepInvocationAssert.CompileRequestedWithTimeout(
            compileService,
            expectedTimeoutMilliseconds: 800,
            expectProgressSink: false);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenRemainingTimeoutIsSubMillisecond_RoundsStepTimeoutUp ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WhenRemainingTimeoutIsSubMillisecond_RoundsStepTimeoutUp));
        var timeProvider = new ManualTimeProvider();
        var project = ProjectIdentityInfoTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var readyService = new RecordingVerifyReadyService(input =>
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(1000) - TimeSpan.FromTicks(1));
            return CreateReadyResult(input.Target, project);
        });
        var compileService = new RecordingVerifyCompileService(_ => CreateCompileResult(project));
        var service = CreateService(
            scope.FullPath,
            readyService: readyService,
            compileService: compileService,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:default",
            ProfilePath: null,
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 1000));

        Assert.True(result.IsSuccess);
        VerifyStepInvocationAssert.CompileRequestedWithTimeout(
            compileService,
            expectedTimeoutMilliseconds: 1);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenLogsStepExceedsRemainingTimeout_ReturnsTimeoutFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WhenLogsStepExceedsRemainingTimeout_ReturnsTimeoutFailure));
        var timeProvider = new ManualTimeProvider();
        var project = ProjectIdentityInfoTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var compileService = new RecordingVerifyCompileService(_ =>
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(999));
            return CreateCompileResult(project, CompileClaimStatusValues.Failed);
        });
        var logsService = new RecordingVerifyLogsUnityService((_, _, cancellationToken) =>
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(LogsReadServiceResult.Success());
        });
        var service = CreateService(
            scope.FullPath,
            compileService: compileService,
            logsService: logsService,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:default",
            ProfilePath: null,
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 1000));

        VerifyServiceAssert.LogsStepTimedOutAfterAttempt(
            result,
            logsService);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenStepReturnsSuccessAfterDeadline_ReturnsTimeoutFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WhenStepReturnsSuccessAfterDeadline_ReturnsTimeoutFailure));
        var timeProvider = new ManualTimeProvider();
        var project = ProjectIdentityInfoTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var compileService = new RecordingVerifyCompileService(_ =>
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(1001));
            return CreateCompileResult(project);
        });
        var service = CreateService(
            scope.FullPath,
            compileService: compileService,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:script",
            ProfilePath: null,
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 1000));

        VerifyServiceAssert.CompileStepTimedOutAfterAttempt(
            result,
            compileService);
    }

}
