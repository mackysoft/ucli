namespace MackySoft.Ucli.Contracts;

/// <summary> Provides the bundled catalog descriptors for error codes owned by <c>Ucli.Contracts</c>. </summary>
public static class UcliKnownErrorDescriptors
{
    /// <summary> Gets bundled descriptors sorted by error code value. </summary>
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } = CreateAll();

    private static UcliErrorDescriptor[] CreateAll ()
    {
        return UcliCoreErrorCodeDescriptors.All
            .Concat(DaemonErrorCodeDescriptors.All)
            .Concat(EditorLifecycleErrorCodeDescriptors.All)
            .Concat(ExecuteRequestErrorCodeDescriptors.All)
            .Concat(IpcProtocolErrorCodeDescriptors.All)
            .Concat(IpcSessionErrorCodeDescriptors.All)
            .Concat(IpcTransportErrorCodeDescriptors.All)
            .Concat(OperationAuthorizationErrorCodeDescriptors.All)
            .Concat(PlanTokenErrorCodeDescriptors.All)
            .Concat(PlayModeErrorCodeDescriptors.All)
            .Concat(ReadIndexErrorCodeDescriptors.All)
            .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
