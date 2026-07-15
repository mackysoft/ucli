using System;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Identifies either one raw step or one primitive lowered from an edit step. </summary>
    internal sealed class OperationExecutionKey : IEquatable<OperationExecutionKey>
    {
        private OperationExecutionKey (
            OperationExecutionKeyKind kind,
            IpcExecuteStepId stepId,
            int primitiveIndex)
        {
            Kind = kind;
            StepId = stepId ?? throw new ArgumentNullException(nameof(stepId));
            PrimitiveIndex = primitiveIndex;
        }

        private OperationExecutionKeyKind Kind { get; }

        public IpcExecuteStepId StepId { get; }

        public int PrimitiveIndex { get; }

        public bool IsEditPrimitive => Kind == OperationExecutionKeyKind.EditPrimitive;

        public static OperationExecutionKey ForRawStep (IpcExecuteStepId stepId)
        {
            return new OperationExecutionKey(OperationExecutionKeyKind.RawStep, stepId, primitiveIndex: -1);
        }

        public static OperationExecutionKey ForEditPrimitive (
            IpcExecuteStepId stepId,
            int primitiveIndex)
        {
            if (primitiveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(primitiveIndex), primitiveIndex, "Primitive index must not be negative.");
            }

            return new OperationExecutionKey(OperationExecutionKeyKind.EditPrimitive, stepId, primitiveIndex);
        }

        public bool Equals (OperationExecutionKey? other)
        {
            return ReferenceEquals(this, other)
                || (other != null
                    && Kind == other.Kind
                    && StepId.Equals(other.StepId)
                    && PrimitiveIndex == other.PrimitiveIndex);
        }

        public override bool Equals (object? obj)
        {
            return obj is OperationExecutionKey other && Equals(other);
        }

        public override int GetHashCode ()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ StepId.GetHashCode();
                return (hashCode * 397) ^ PrimitiveIndex;
            }
        }

        private enum OperationExecutionKeyKind : byte
        {
            RawStep = 0,
            EditPrimitive = 1,
        }
    }
}
