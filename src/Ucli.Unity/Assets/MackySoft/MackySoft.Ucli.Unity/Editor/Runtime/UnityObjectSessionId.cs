using System;
using System.Globalization;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Represents the native identity of one live Unity object within the current Editor session.
    /// </summary>
    /// <remarks>
    /// This identity is suitable for in-memory equality, dictionary keys, and request-local text.
    /// It is not a persistent identifier and must not be stored across Editor sessions.
    /// </remarks>
    internal sealed class UnityObjectSessionId : IEquatable<UnityObjectSessionId>
    {
#if UNITY_6000_5_OR_NEWER
        private readonly EntityId value;

        private UnityObjectSessionId (EntityId value)
        {
            this.value = value;
        }
#else
        private readonly int value;

        private UnityObjectSessionId (int value)
        {
            this.value = value;
        }
#endif

        /// <summary> Captures the complete native identity of a live Unity object. </summary>
        public static UnityObjectSessionId Create (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentException(
                    "A live Unity object is required to create a session identity.",
                    nameof(unityObject));
            }

#if UNITY_6000_5_OR_NEWER
            return new UnityObjectSessionId(unityObject.GetEntityId());
#else
            return new UnityObjectSessionId(unityObject.GetInstanceID());
#endif
        }

        public bool Equals (UnityObjectSessionId other)
        {
            return other != null && value.Equals(other.value);
        }

        public override bool Equals (object obj)
        {
            return obj is UnityObjectSessionId other && Equals(other);
        }

        public override int GetHashCode ()
        {
            return value.GetHashCode();
        }

        public override string ToString ()
        {
#if UNITY_6000_5_OR_NEWER
            return value.ToString();
#else
            return value.ToString(CultureInfo.InvariantCulture);
#endif
        }
    }
}
