namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Defines error kinds for required object-property reads in operation contracts. </summary>
internal enum OperationObjectReadErrorKind
{
    /// <summary> The required object property is missing. </summary>
    Missing,

    /// <summary> The property exists but is not a JSON object. </summary>
    TypeMismatch,
}