namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one captured project-settings file state used by project-domain operations. </summary>
    internal readonly struct ProjectOperationFileSnapshot : System.IEquatable<ProjectOperationFileSnapshot>
    {
        /// <summary> Initializes a new instance of the <see cref="ProjectOperationFileSnapshot" /> struct. </summary>
        /// <param name="size"> The file size in bytes. </param>
        /// <param name="lastWriteUtcTicks"> The last-write timestamp in UTC ticks. </param>
        public ProjectOperationFileSnapshot (
            long size,
            long lastWriteUtcTicks)
        {
            Size = size;
            LastWriteUtcTicks = lastWriteUtcTicks;
        }

        /// <summary> Gets the file size in bytes. </summary>
        public long Size { get; }

        /// <summary> Gets the last-write timestamp in UTC ticks. </summary>
        public long LastWriteUtcTicks { get; }

        /// <summary> Determines whether the current value equals the specified snapshot. </summary>
        /// <param name="other"> The other snapshot value. </param>
        /// <returns> <see langword="true" /> when both values are equal; otherwise <see langword="false" />. </returns>
        public bool Equals (ProjectOperationFileSnapshot other)
        {
            return Size == other.Size
                && LastWriteUtcTicks == other.LastWriteUtcTicks;
        }

        /// <summary> Determines whether the current value equals the specified object. </summary>
        /// <param name="obj"> The other object instance. </param>
        /// <returns> <see langword="true" /> when both values are equal; otherwise <see langword="false" />. </returns>
        public override bool Equals (object obj)
        {
            return obj is ProjectOperationFileSnapshot other && Equals(other);
        }

        /// <summary> Returns one hash code for the current value. </summary>
        /// <returns> The hash code value. </returns>
        public override int GetHashCode ()
        {
            unchecked
            {
                return (Size.GetHashCode() * 397) ^ LastWriteUtcTicks.GetHashCode();
            }
        }
    }
}
