namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Defines machine-readable read-error kinds for one step property. </summary>
internal enum StepPropertyReadErrorKind
{
    None = 0,

    Missing,

    TypeMismatch,
}
