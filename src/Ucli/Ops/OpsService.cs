using System.Globalization;
using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Ops;

/// <summary> Implements the <c>ops</c> command workflow with read-index first and Unity live fallback semantics. </summary>
internal sealed class OpsService : IOpsService
{
    private const string FreshnessFresh = "fresh";

    private const string FreshnessProbable = "probable";

    private const string FreshnessStale = "stale";

    private const string SourceIndex = "index";

    private const string SourceUnity = "unity";

    private readonly IInitStatusContextResolver initStatusContextResolver;

    private readonly IIndexCatalogReader indexCatalogReader;

    private readonly IIndexFreshnessEvaluator indexFreshnessEvaluator;

    private readonly IIndexInputFingerprintCalculator indexInputFingerprintCalculator;

    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    private readonly IOpsCatalogLiveReader opsCatalogLiveReader;

    private readonly IOpsCatalogStore opsCatalogStore;

    /// <summary> Initializes a new instance of the <see cref="OpsService" /> class. </summary>
    /// <param name="initStatusContextResolver"> The shared init/status context resolver dependency. </param>
    /// <param name="indexCatalogReader"> The read-index catalog reader dependency. </param>
    /// <param name="indexFreshnessEvaluator"> The read-index freshness evaluator dependency. </param>
    /// <param name="indexInputFingerprintCalculator"> The read-index input fingerprint calculator dependency. </param>
    /// <param name="modeDecisionService"> The Unity execution-mode decision service dependency. </param>
    /// <param name="opsCatalogLiveReader"> The live catalog reader dependency. </param>
    /// <param name="opsCatalogStore"> The ops catalog persistence dependency. </param>
    public OpsService (
        IInitStatusContextResolver initStatusContextResolver,
        IIndexCatalogReader indexCatalogReader,
        IIndexFreshnessEvaluator indexFreshnessEvaluator,
        IIndexInputFingerprintCalculator indexInputFingerprintCalculator,
        IUnityExecutionModeDecisionService modeDecisionService,
        IOpsCatalogLiveReader opsCatalogLiveReader,
        IOpsCatalogStore opsCatalogStore)
    {
        this.initStatusContextResolver = initStatusContextResolver ?? throw new ArgumentNullException(nameof(initStatusContextResolver));
        this.indexCatalogReader = indexCatalogReader ?? throw new ArgumentNullException(nameof(indexCatalogReader));
        this.indexFreshnessEvaluator = indexFreshnessEvaluator ?? throw new ArgumentNullException(nameof(indexFreshnessEvaluator));
        this.indexInputFingerprintCalculator = indexInputFingerprintCalculator ?? throw new ArgumentNullException(nameof(indexInputFingerprintCalculator));
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        this.opsCatalogLiveReader = opsCatalogLiveReader ?? throw new ArgumentNullException(nameof(opsCatalogLiveReader));
        this.opsCatalogStore = opsCatalogStore ?? throw new ArgumentNullException(nameof(opsCatalogStore));
    }

    /// <inheritdoc />
    public async ValueTask<OpsServiceResult<OpsListExecutionOutput>> List (
        OpsCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var catalogResult = await ReadCatalog(input, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsServiceResult<OpsListExecutionOutput>.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!);
        }

        var output = catalogResult.Output!;
        var operations = output.Operations
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .Select(static operation => new OpsOperationListItem(
                Name: operation.Name!,
                Kind: operation.Kind!,
                Policy: operation.Policy!))
            .ToArray();

        return OpsServiceResult<OpsListExecutionOutput>.Success(
            new OpsListExecutionOutput(
                Operations: operations,
                ReadIndex: output.ReadIndex),
            "uCLI ops list completed.");
    }

    /// <inheritdoc />
    public async ValueTask<OpsServiceResult<OpsDescribeExecutionOutput>> Describe (
        OpsDescribeCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.OperationName))
        {
            return OpsServiceResult<OpsDescribeExecutionOutput>.Failure(
                "Operation name must not be empty.",
                IpcErrorCodes.InvalidArgument);
        }

        var catalogResult = await ReadCatalog(
                new OpsCommandInput(
                    ProjectPath: input.ProjectPath,
                    Mode: input.Mode,
                    Timeout: input.Timeout,
                    ReadIndexMode: input.ReadIndexMode),
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsServiceResult<OpsDescribeExecutionOutput>.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!);
        }

        var operation = catalogResult.Output!.Operations
            .FirstOrDefault(operation => string.Equals(operation.Name, input.OperationName, StringComparison.Ordinal));
        if (operation == null)
        {
            return OpsServiceResult<OpsDescribeExecutionOutput>.Failure(
                $"Operation '{input.OperationName}' is not available.",
                IpcErrorCodes.InvalidArgument);
        }

        if (!TryParseSchema(operation.ArgsSchemaJson!, out var argsSchema))
        {
            return OpsServiceResult<OpsDescribeExecutionOutput>.Failure(
                $"Operation '{input.OperationName}' args schema is invalid.",
                IpcErrorCodes.InternalError);
        }

        return OpsServiceResult<OpsDescribeExecutionOutput>.Success(
            new OpsDescribeExecutionOutput(
                Operation: new OpsOperationDetail(
                    Name: operation.Name!,
                    Kind: operation.Kind!,
                    Policy: operation.Policy!,
                    ArgsSchema: argsSchema),
                ReadIndex: catalogResult.Output.ReadIndex),
            $"uCLI ops describe completed for '{input.OperationName}'.");
    }

    private async ValueTask<OpsServiceResult<OpsCatalogReadOutput>> ReadCatalog (
        OpsCommandInput input,
        CancellationToken cancellationToken)
    {
        var contextResult = await initStatusContextResolver.Resolve(
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return FromExecutionError<OpsCatalogReadOutput>(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, context.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            return FromExecutionError<OpsCatalogReadOutput>(readIndexModeResult.Error!);
        }

        var readIndexMode = readIndexModeResult.Mode!.Value;
        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadLiveCatalog(
                    context,
                    input,
                    "readIndex disabled by mode.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var opsCatalogResult = await indexCatalogReader.ReadOpsCatalog(
                context.UnityProject.RepositoryRoot,
                context.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!opsCatalogResult.IsSuccess)
        {
            if (string.Equals(opsCatalogResult.Error!.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            {
                return OpsServiceResult<OpsCatalogReadOutput>.Failure(
                    opsCatalogResult.Error.Message,
                    IpcErrorCodes.InvalidArgument);
            }

            return await ReadLiveCatalog(
                    context,
                    input,
                    opsCatalogResult.Error.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await indexFreshnessEvaluator.Evaluate(
                context.UnityProject.RepositoryRoot,
                context.UnityProject.ProjectFingerprint,
                context.UnityProject.UnityProjectRoot,
                ReadIndexMode.AllowStale,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return OpsServiceResult<OpsCatalogReadOutput>.Failure(
                freshnessResult.Error!.Message,
                freshnessResult.Error.Code);
        }

        if (readIndexMode == ReadIndexMode.AllowStale || freshnessResult.Freshness == IndexFreshness.Fresh)
        {
            return OpsServiceResult<OpsCatalogReadOutput>.Success(
                new OpsCatalogReadOutput(
                    Operations: opsCatalogResult.Value!.Entries!
                        .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
                        .ToArray(),
                    ReadIndex: new OpsReadIndexInfo(
                        Used: true,
                        Hit: true,
                        Source: SourceIndex,
                        Freshness: ToFreshnessValue(freshnessResult.Freshness),
                        GeneratedAtUtc: opsCatalogResult.Value.GeneratedAtUtc,
                        FallbackReason: null)),
                "Read-index ops catalog hit.");
        }

        return await ReadLiveCatalog(
                context,
                input,
                $"Existing ops index freshness is '{ToFreshnessValue(freshnessResult.Freshness)}'.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<OpsServiceResult<OpsCatalogReadOutput>> ReadLiveCatalog (
        InitStatusContext context,
        OpsCommandInput input,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(input.Timeout, UcliCommandIds.Ops, context.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return FromExecutionError<OpsCatalogReadOutput>(timeoutResolutionResult.Error!);
        }

        var timeout = timeoutResolutionResult.Timeout!.Value;
        var normalizedTimeout = checked((int)timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        var modeDecisionResult = await modeDecisionService.Decide(
                UcliCommandIds.Ops,
                input.Mode,
                normalizedTimeout,
                context.Config,
                context.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (modeDecisionResult.HasContractError)
        {
            return OpsServiceResult<OpsCatalogReadOutput>.Failure(
                modeDecisionResult.ContractError!.Message,
                modeDecisionResult.ContractError.Code);
        }

        if (!modeDecisionResult.IsSuccess)
        {
            return FromExecutionError<OpsCatalogReadOutput>(modeDecisionResult.Error!);
        }

        var liveReadResult = await opsCatalogLiveReader.Read(
                context.UnityProject,
                modeDecisionResult.Decision!.Target,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!liveReadResult.IsSuccess)
        {
            return OpsServiceResult<OpsCatalogReadOutput>.Failure(
                liveReadResult.Message,
                liveReadResult.ErrorCode!);
        }

        var response = liveReadResult.Response!;
        var operations = response.Operations!
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .ToArray();
        var persistFailure = await TryPersistLiveCatalog(
                context,
                response.GeneratedAtUtc,
                operations,
                cancellationToken)
            .ConfigureAwait(false);

        return OpsServiceResult<OpsCatalogReadOutput>.Success(
            new OpsCatalogReadOutput(
                Operations: operations,
                ReadIndex: new OpsReadIndexInfo(
                    Used: false,
                    Hit: true,
                    Source: SourceUnity,
                    Freshness: FreshnessFresh,
                    GeneratedAtUtc: response.GeneratedAtUtc,
                    FallbackReason: CombineFallbackReasons(fallbackReason, persistFailure))),
            "Live ops catalog read completed.");
    }

    private async ValueTask<string?> TryPersistLiveCatalog (
        InitStatusContext context,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputSnapshot = await indexInputFingerprintCalculator.TryCompute(
                context.UnityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (inputSnapshot == null)
        {
            return "Failed to persist refreshed ops readIndex because input fingerprint could not be computed.";
        }

        try
        {
            await opsCatalogStore.Write(
                    context.UnityProject.RepositoryRoot,
                    context.UnityProject.ProjectFingerprint,
                    generatedAtUtc,
                    operations,
                    inputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // NOTE:
            // Live results remain authoritative even when local read-index persistence fails.
            // The failure is surfaced in payload.readIndex.fallbackReason instead of failing the command.
            return $"Failed to persist refreshed ops readIndex. {exception.Message}";
        }
    }

    private static OpsServiceResult<T> FromExecutionError<T> (ExecutionError error)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(error);
        var errorCode = ExecutionErrorKindCodeMapper.ToCode(error.Kind);

        return OpsServiceResult<T>.Failure(error.Message, errorCode);
    }

    private static bool TryParseSchema (
        string json,
        out JsonElement schema)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                schema = default;
                return false;
            }

            schema = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            schema = default;
            return false;
        }
    }

    private static string? CombineFallbackReasons (
        string? first,
        string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first} {second}";
    }

    private static string ToFreshnessValue (IndexFreshness freshness)
    {
        return freshness switch
        {
            IndexFreshness.Fresh => FreshnessFresh,
            IndexFreshness.Probable => FreshnessProbable,
            IndexFreshness.Stale => FreshnessStale,
            _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "Unsupported index freshness."),
        };
    }
}