namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation registration entry. </summary>
    internal readonly struct UcliOperationRegistration
    {
        /// <summary> Initializes a new instance of the <see cref="UcliOperationRegistration" /> struct. </summary>
        /// <param name="metadata"> The operation metadata. </param>
        /// <param name="operation"> The operation implementation instance. </param>
        public UcliOperationRegistration (
            UcliOperationMetadata metadata,
            IUcliOperation operation)
        {
            Metadata = metadata;
            Operation = operation;
        }

        /// <summary> Gets the operation metadata. </summary>
        public UcliOperationMetadata Metadata { get; }

        /// <summary> Gets the operation implementation instance. </summary>
        public IUcliOperation Operation { get; }
    }
}
