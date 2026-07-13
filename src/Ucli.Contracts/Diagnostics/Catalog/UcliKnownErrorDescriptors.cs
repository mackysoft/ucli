namespace MackySoft.Ucli.Contracts;

/// <summary> Provides the bundled catalog descriptors for error codes owned by <c>Ucli.Contracts</c>. </summary>
public static class UcliKnownErrorDescriptors
{
    private static readonly IReadOnlyList<UcliErrorDescriptor>[] DescriptorGroups =
    [
        UcliCoreErrorCodeDescriptors.All,
        BuildErrorCodeDescriptors.All,
        DaemonErrorCodeDescriptors.All,
        EditorLifecycleErrorCodeDescriptors.All,
        ExecuteRequestErrorCodeDescriptors.All,
        IpcProtocolErrorCodeDescriptors.All,
        IpcSessionErrorCodeDescriptors.All,
        IpcTransportErrorCodeDescriptors.All,
        OperationAuthorizationErrorCodeDescriptors.All,
        PlanTokenErrorCodeDescriptors.All,
        PlayModeErrorCodeDescriptors.All,
        ReadIndexErrorCodeDescriptors.All,
        ScreenshotErrorCodeDescriptors.All,
    ];

    /// <summary> Gets bundled descriptors sorted by error code value. </summary>
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } = CreateAll();

    private static UcliErrorDescriptor[] CreateAll ()
    {
        return DescriptorGroups
            .SelectMany(static descriptors => descriptors)
            .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
