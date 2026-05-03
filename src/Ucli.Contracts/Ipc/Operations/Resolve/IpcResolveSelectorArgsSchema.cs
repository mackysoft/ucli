namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the JSON schema for <c>ucli.resolve</c> operation arguments. </summary>
public static class IpcResolveSelectorArgsSchema
{
    /// <summary> Gets the JSON schema text for <c>ucli.resolve</c> operation arguments. </summary>
    public static string Json => UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(typeof(ResolveSelectorArgs));
}
