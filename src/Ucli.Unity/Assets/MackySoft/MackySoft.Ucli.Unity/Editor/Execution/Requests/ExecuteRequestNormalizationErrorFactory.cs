using MackySoft.Ucli.Contracts.Ipc.Validation;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Creates normalization errors for contract-read failures. </summary>
    internal static class ExecuteRequestNormalizationErrorFactory
    {
        public static ExecuteRequestNormalizationError RequestId (JsonStringReadError readError)
        {
            return readError.Kind switch
            {
                JsonStringReadErrorKind.Missing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' is required.",
                    null),
                JsonStringReadErrorKind.TypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be a UUID string.",
                    null),
                JsonStringReadErrorKind.EmptyOrWhitespace or JsonStringReadErrorKind.OuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must not contain leading or trailing whitespace.",
                    null),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' is invalid.",
                    null),
            };
        }

        public static ExecuteRequestNormalizationError OperationId (
            int operationIndex,
            JsonStringReadError readError)
        {
            return readError.Kind switch
            {
                JsonStringReadErrorKind.Missing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {operationIndex} requires property 'id'.",
                    null),
                JsonStringReadErrorKind.TypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {operationIndex} property 'id' must be a string.",
                    null),
                JsonStringReadErrorKind.EmptyOrWhitespace or JsonStringReadErrorKind.OuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {operationIndex} property 'id' must not be empty or contain outer whitespace.",
                    null),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {operationIndex} property 'id' is invalid.",
                    null),
            };
        }

        public static ExecuteRequestNormalizationError OperationName (
            string operationId,
            JsonStringReadError readError)
        {
            return readError.Kind switch
            {
                JsonStringReadErrorKind.Missing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' requires property 'op'.",
                    operationId),
                JsonStringReadErrorKind.TypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'op' must be a string.",
                    operationId),
                JsonStringReadErrorKind.EmptyOrWhitespace or JsonStringReadErrorKind.OuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'op' must not be empty or contain outer whitespace.",
                    operationId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'op' is invalid.",
                    operationId),
            };
        }

        public static ExecuteRequestNormalizationError OperationArgs (
            string operationId,
            OperationObjectReadErrorKind readError)
        {
            return readError switch
            {
                OperationObjectReadErrorKind.Missing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' requires property 'args'.",
                    operationId),
                OperationObjectReadErrorKind.TypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'args' must be an object.",
                    operationId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'args' is invalid.",
                    operationId),
            };
        }

        public static ExecuteRequestNormalizationError OperationAlias (
            string operationId,
            JsonStringReadError readError)
        {
            return readError.Kind switch
            {
                JsonStringReadErrorKind.TypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'as' must be a string when specified.",
                    operationId),
                JsonStringReadErrorKind.EmptyOrWhitespace or JsonStringReadErrorKind.OuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'as' must not be empty or contain outer whitespace.",
                    operationId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'as' is invalid.",
                    operationId),
            };
        }

        public static ExecuteRequestNormalizationError OperationExpectation (
            string operationId,
            ExpectationConstraintReadError readError)
        {
            return readError.Kind switch
            {
                ExpectationConstraintReadErrorKind.ExpectationMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' must be an object when specified.",
                    operationId),
                ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' contains an unknown property: {readError.UnknownPropertyName}.",
                    operationId),
                ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' must contain at least one constraint.",
                    operationId),
                ExpectationConstraintReadErrorKind.BooleanConstraintMustBeBoolean => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property '{readError.PropertyPath}' must be a boolean.",
                    operationId),
                ExpectationConstraintReadErrorKind.IntegerConstraintMustBeInteger => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property '{readError.PropertyPath}' must be an integer.",
                    operationId),
                ExpectationConstraintReadErrorKind.IntegerConstraintMustBeNonNegative => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property '{readError.PropertyPath}' must be greater than or equal to 0.",
                    operationId),
                ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' cannot combine 'count' with 'min' or 'max'.",
                    operationId),
                ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' requires 'min' to be less than or equal to 'max'.",
                    operationId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' is invalid.",
                    operationId),
            };
        }
    }
}
