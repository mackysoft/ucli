using System.Collections.Concurrent;

namespace MackySoft.Ucli.Cli
{
    /// <summary> Buffers argument parse error messages emitted before command handlers start. </summary>
    internal static class ParseErrorBuffer
    {
        private static readonly ConcurrentQueue<string> MessagesCore = new();

        /// <summary> Gets the buffered parse error messages. </summary>
        /// <returns> A snapshot enumeration of messages currently stored in the buffer. </returns>
        public static IEnumerable<string> Messages => MessagesCore;

        /// <summary> Gets a value indicating whether at least one parse error message is buffered. </summary>
        public static bool HasAny => !MessagesCore.IsEmpty;

        /// <summary> Adds a parse error message to the buffer. </summary>
        /// <param name="message"> The parse error message to store. </param>
        public static void Add (string message)
        {
            MessagesCore.Enqueue(message);
        }

        /// <summary> Removes all buffered parse error messages. </summary>
        public static void Clear ()
        {
            while (MessagesCore.TryDequeue(out _))
            {
            }
        }
    }
}