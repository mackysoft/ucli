namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines allowed operation safety levels configured in <c>.ucli/config.json</c>. </summary>
public enum OperationPolicy
{
    /// <summary> Allows only safe operations. </summary>
    Safe = 0,

    /// <summary> Allows safe and advanced operations. </summary>
    Advanced = 1,

    /// <summary> Allows safe, advanced, and dangerous operations. </summary>
    Dangerous = 2,
}
