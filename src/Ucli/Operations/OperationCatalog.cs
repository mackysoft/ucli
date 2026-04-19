using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Operations;

/// <summary> Provides operation lookup and listing backed by one provider snapshot. </summary>
internal sealed class OperationCatalog : IOperationCatalog
{
    private readonly IOperationCatalogProvider provider;

    private readonly object syncRoot = new();

    private Task<CatalogSnapshot>? snapshotTask;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalog" /> class. </summary>
    /// <param name="provider"> The operation metadata provider. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="provider" /> is <see langword="null" />. </exception>
    public OperationCatalog (IOperationCatalogProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary> Asynchronously gets one operation descriptor by operation name. </summary>
    /// <param name="name"> The operation name to resolve. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> A task that resolves to the operation descriptor. </para>
    /// <para> Returns <see langword="null" /> when the operation does not exist. </para>
    /// </returns>
    /// <exception cref="InvalidOperationException"> Thrown when provider data includes duplicated or invalid operation names. </exception>
    public async ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var snapshot = await GetSnapshot(cancellationToken).ConfigureAwait(false);
        if (snapshot.OperationsByName.TryGetValue(name, out var descriptor))
        {
            return descriptor;
        }

        return null;
    }

    /// <summary> Asynchronously gets all registered operation descriptors. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the descriptor list ordered by operation name. </returns>
    /// <exception cref="InvalidOperationException"> Thrown when provider data includes duplicated or invalid operation names. </exception>
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await GetSnapshot(cancellationToken).ConfigureAwait(false);
        return snapshot.SortedOperations;
    }

    /// <summary> Asynchronously gets all registered operation descriptors for the specified resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="config"> The loaded configuration used to execute catalog discovery. </param>
    /// <param name="failFast"> Whether live catalog discovery should fail immediately instead of waiting for Unity readiness. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the descriptor list ordered by operation name. </returns>
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);

        var loadedOperations = await provider.GetOperations(unityProject, config, mode, timeout, failFast, cancellationToken).ConfigureAwait(false);
        var snapshot = CreateSnapshot(loadedOperations, cancellationToken);
        return snapshot.SortedOperations;
    }

    /// <summary> Gets or creates the immutable catalog snapshot. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the loaded catalog snapshot. </returns>
    private async ValueTask<CatalogSnapshot> GetSnapshot (CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task<CatalogSnapshot> initializationTask;
        lock (syncRoot)
        {
            snapshotTask ??= BuildSnapshot(cancellationToken);
            initializationTask = snapshotTask;
        }

        try
        {
            return await initializationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (initializationTask.IsFaulted || initializationTask.IsCanceled)
            {
                lock (syncRoot)
                {
                    if (ReferenceEquals(snapshotTask, initializationTask))
                    {
                        snapshotTask = null;
                    }
                }
            }

            throw;
        }
    }

    /// <summary> Builds the immutable catalog snapshot from provider data. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the built catalog snapshot. </returns>
    /// <exception cref="InvalidOperationException"> Thrown when provider data includes duplicated or invalid operation names. </exception>
    private async Task<CatalogSnapshot> BuildSnapshot (CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var loadedOperations = await provider.GetOperations(cancellationToken).ConfigureAwait(false);
        return CreateSnapshot(loadedOperations, cancellationToken);
    }

    /// <summary> Builds one immutable catalog snapshot from provider data. </summary>
    /// <param name="loadedOperations"> The loaded descriptor sequence. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The built catalog snapshot. </returns>
    /// <exception cref="InvalidOperationException"> Thrown when provider data includes duplicated or invalid operation names. </exception>
    private static CatalogSnapshot CreateSnapshot (
        IReadOnlyList<UcliOperationDescriptor> loadedOperations,
        CancellationToken cancellationToken)
    {
        if (loadedOperations is null)
        {
            throw new InvalidOperationException("Operation catalog provider returned null.");
        }

        var operationsByName = new Dictionary<string, UcliOperationDescriptor>(StringComparer.Ordinal);
        for (var i = 0; i < loadedOperations.Count; i++)
        {
            var operation = loadedOperations[i];
            cancellationToken.ThrowIfCancellationRequested();

            if (operation is null)
            {
                throw new InvalidOperationException("Operation catalog provider contains a null descriptor.");
            }

            if (string.IsNullOrWhiteSpace(operation.Name))
            {
                throw new InvalidOperationException("Operation name must not be null, empty, or whitespace.");
            }

            if (!operationsByName.TryAdd(operation.Name, operation))
            {
                throw new InvalidOperationException($"Operation name is duplicated: {operation.Name}.");
            }
        }

        var sortedOperations = operationsByName
            .Values
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .ToArray();
        return new CatalogSnapshot(sortedOperations, operationsByName);
    }

    /// <summary> Represents one immutable lookup snapshot for catalog data. </summary>
    /// <param name="SortedOperations"> The ordered descriptor list. </param>
    /// <param name="OperationsByName"> The descriptor lookup by operation name. </param>
    private readonly record struct CatalogSnapshot (
        IReadOnlyList<UcliOperationDescriptor> SortedOperations,
        Dictionary<string, UcliOperationDescriptor> OperationsByName);
}