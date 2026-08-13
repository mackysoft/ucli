using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary>
/// Fixes one Program Run host and proves its generation before Program
/// persistence can admit a Step. The returned binding remains caller-owned so
/// every initial dispatch uses the same selected host.
/// </summary>
internal interface IProgramRunHostContextResolver
{
    ValueTask<ProgramRunHostContextResolution> ResolveAsync (
        ProjectContext project,
        UnityExecutionMode requestedMode,
        ExecutionDeadline deadline,
        IpcProgramEffectiveAuthorizationSnapshot authorization,
        ProgramGuiRequirement? guiRequirement,
        CancellationToken cancellationToken = default);
}

/// <summary> Represents a verified fixed host binding and its Program registration observation. </summary>
internal sealed class ProgramRunHostContext : IAsyncDisposable
{
    public ProgramRunHostContext (
        ProjectContext project,
        IUnityExecutionHostBinding binding,
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot generation)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    /// <summary>Gets the configuration and project context fixed for this Program Run.</summary>
    public ProjectContext Project { get; }

    public IUnityExecutionHostBinding Binding { get; }

    public LifecycleExecutionHostRegistration Host { get; }

    public UnityEditorGenerationSnapshot Generation { get; }

    public ValueTask DisposeAsync () => Binding.DisposeAsync();
}

/// <summary> Contains either one fixed Program host context or the reason it could not be proved. </summary>
internal sealed record ProgramRunHostContextResolution (
    ProgramRunHostContext? Context,
    ApplicationFailure? Failure)
{
    public bool IsSuccess => Context is not null;

    public static ProgramRunHostContextResolution Success (ProgramRunHostContext context) =>
        new(context ?? throw new ArgumentNullException(nameof(context)), null);

    public static ProgramRunHostContextResolution Failed (ApplicationFailure failure) =>
        new(null, failure ?? throw new ArgumentNullException(nameof(failure)));
}

/// <summary> Identifies the first Program Step that requires a GUI Editor host. </summary>
internal sealed record ProgramGuiRequirement (int StepIndex, string Command, UcliCode ErrorCode)
{
    public string InstancePath => $"/steps/{StepIndex}";

    public static ProgramGuiRequirement? Find (ProgramDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        for (var index = 0; index < definition.Steps.Count; index++)
        {
            var step = definition.Steps[index];
            if (step is PlayEnterProgramStep or PlayExitProgramStep)
            {
                return new ProgramGuiRequirement(index, step is PlayEnterProgramStep ? "play.enter" : "play.exit", PlayModeErrorCodes.PlayModeRequiresGuiEditor);
            }
            if (step is ScreenshotGameProgramStep or ScreenshotSceneProgramStep)
            {
                return new ProgramGuiRequirement(index, step is ScreenshotGameProgramStep ? "screenshot.game" : "screenshot.scene", ScreenshotErrorCodes.ScreenshotRequiresGuiSession);
            }
        }
        return null;
    }
}

/// <summary> Implements Program registration host selection without dispatching any Program Step. </summary>
internal sealed class ProgramRunHostContextResolver : IProgramRunHostContextResolver
{
    private readonly IUnityExecutionModeDecisionService modeDecisionService;
    private readonly ILifecycleExecutionHostBindingFactory bindingFactory;

    public ProgramRunHostContextResolver (
        IUnityExecutionModeDecisionService modeDecisionService,
        ILifecycleExecutionHostBindingFactory bindingFactory)
    {
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        this.bindingFactory = bindingFactory ?? throw new ArgumentNullException(nameof(bindingFactory));
    }

    public async ValueTask<ProgramRunHostContextResolution> ResolveAsync (
        ProjectContext project,
        UnityExecutionMode requestedMode,
        ExecutionDeadline deadline,
        IpcProgramEffectiveAuthorizationSnapshot authorization,
        ProgramGuiRequirement? guiRequirement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(deadline);
        ArgumentNullException.ThrowIfNull(authorization);
        if (guiRequirement is not null && requestedMode == UnityExecutionMode.Oneshot)
        {
            return ProgramRunHostContextResolution.Failed(ApplicationFailure.EnvironmentError(
                $"Program Step '{guiRequirement.Command}' requires a GUI Editor host.", guiRequirement.ErrorCode, guiRequirement.InstancePath));
        }
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return ProgramRunHostContextResolution.Failed(ApplicationFailure.Timeout(
                "Program execution deadline elapsed before its fixed Unity host was selected.",
                LifecycleExecutionErrorCodes.DeadlineExceeded));
        }

        var decision = await modeDecisionService.DecideAsync(
                guiRequirement is null ? requestedMode : UnityExecutionMode.Daemon,
                project.UnityProject,
                modeDecisionTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!decision.IsSuccess)
        {
            var failure = decision.HasContractError
                ? ApplicationFailure.EnvironmentError(decision.ContractError!.Message, decision.ContractError.Code)
                : ApplicationFailure.FromExecutionError(decision.Error!);
            return ProgramRunHostContextResolution.Failed(failure);
        }

        var bindingResolution = await bindingFactory.BindResolvedTargetAsync(
                project.UnityProject,
                decision.Decision!.Target,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bindingResolution.IsSuccess)
        {
            return ProgramRunHostContextResolution.Failed(ApplicationFailure.FromCode(
                bindingResolution.Failure!.Code,
                bindingResolution.Failure.Message));
        }

        var binding = bindingResolution.Binding!;
        try
        {
            var execution = await binding.ExecuteAsync(
                    UcliCommandIds.ProgramRun,
                    new UnityRequestPayload.ProgramExecutionContext(authorization),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!execution.IsSuccess)
            {
                return await DisposeFailureAsync(binding, execution.FailureInfo!).ConfigureAwait(false);
            }
            if (execution.Response!.Errors.Count != 0)
            {
                return await DisposeFailureAsync(binding, ApplicationFailure.EnvironmentError(
                    execution.Response.Errors[0].Message))
                    .ConfigureAwait(false);
            }
            if (!IpcPayloadCodec.TryDeserialize(
                    execution.Response.Payload,
                    out IpcProgramExecutionContextResponse response,
                    out var payloadError))
            {
                return await DisposeFailureAsync(binding, ApplicationFailure.EnvironmentError(
                    payloadError.Message))
                    .ConfigureAwait(false);
            }
            if (response.Authorization != authorization)
            {
                return await DisposeFailureAsync(binding, ApplicationFailure.EnvironmentError(
                    "Unity Program execution authorization does not match the CLI-approved snapshot."))
                    .ConfigureAwait(false);
            }
            if (!HasSameConfiguration(CreateExpectedConfiguration(project), response.Configuration))
            {
                return await DisposeFailureAsync(binding, ApplicationFailure.EnvironmentError(
                    "Unity Program execution configuration does not match the CLI-resolved effective configuration."))
                    .ConfigureAwait(false);
            }

            return ProgramRunHostContextResolution.Success(new ProgramRunHostContext(
                project,
                binding,
                response.Host,
                response.Generation));
        }
        catch
        {
            await binding.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask<ProgramRunHostContextResolution> DisposeFailureAsync (
        IUnityExecutionHostBinding binding,
        UnityRequestFailure failure)
    {
        await binding.DisposeAsync().ConfigureAwait(false);
        return ProgramRunHostContextResolution.Failed(ApplicationFailure.FromCode(failure.Code, failure.Message));
    }

    private static async ValueTask<ProgramRunHostContextResolution> DisposeFailureAsync (
        IUnityExecutionHostBinding binding,
        ApplicationFailure failure)
    {
        await binding.DisposeAsync().ConfigureAwait(false);
        return ProgramRunHostContextResolution.Failed(failure);
    }

    private static IpcProgramEffectiveConfigurationSnapshot CreateExpectedConfiguration (ProjectContext project)
    {
        var configuration = project.Config;
        var effectiveTimeouts = IpcTimeoutDefaults.SupportedCommands.ToDictionary(
            static command => command.Name,
            command => checked((int)IpcCommandTimeoutResolver.ResolveNormalized(null, command, configuration).Timeout!.Value.TotalMilliseconds),
            StringComparer.Ordinal);
        return new IpcProgramEffectiveConfigurationSnapshot(
            configuration.SchemaVersion,
            TextVocabulary.GetText(configuration.OperationPolicy),
            TextVocabulary.GetText(configuration.PlanTokenMode),
            TextVocabulary.GetText(configuration.ReadIndexDefaultMode),
            configuration.OperationAllowlist,
            configuration.IpcDefaultTimeoutMilliseconds,
            effectiveTimeouts,
            evalEnabled: configuration.EvalEnabled,
            IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
                configuration.SchemaVersion,
                TextVocabulary.GetText(configuration.OperationPolicy),
                TextVocabulary.GetText(configuration.PlanTokenMode),
                TextVocabulary.GetText(configuration.ReadIndexDefaultMode),
                configuration.OperationAllowlist,
                configuration.IpcDefaultTimeoutMilliseconds,
                effectiveTimeouts,
                evalEnabled: configuration.EvalEnabled));
    }

    private static bool HasSameConfiguration (
        IpcProgramEffectiveConfigurationSnapshot expected,
        IpcProgramEffectiveConfigurationSnapshot actual)
    {
        return expected.Digest == actual.Digest
            && expected.SchemaVersion == actual.SchemaVersion
            && expected.OperationPolicy == actual.OperationPolicy
            && expected.PlanTokenMode == actual.PlanTokenMode
            && expected.ReadIndexDefaultMode == actual.ReadIndexDefaultMode
            && expected.IpcDefaultTimeoutMilliseconds == actual.IpcDefaultTimeoutMilliseconds
            && expected.EvalEnabled == actual.EvalEnabled
            && expected.OperationAllowlist.SequenceEqual(actual.OperationAllowlist, StringComparer.Ordinal)
            && expected.IpcTimeoutMillisecondsByCommand.Count == actual.IpcTimeoutMillisecondsByCommand.Count
            && expected.IpcTimeoutMillisecondsByCommand.All(entry =>
                actual.IpcTimeoutMillisecondsByCommand.TryGetValue(entry.Key, out var value) && value == entry.Value);
    }
}
