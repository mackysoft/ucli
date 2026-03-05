using System;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Collects normalized index schema properties from one serialized object instance. </summary>
    internal interface IIndexSchemaPropertyCollector
    {
        /// <summary> Collects schema properties for one serialized object instance and root type. </summary>
        /// <param name="rootType"> The serialized root runtime type. </param>
        /// <param name="serializedObject"> The serialized object instance. </param>
        /// <returns> The collected property result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any argument is <see langword="null" />. </exception>
        IndexSchemaPropertyCollectionResult Collect (
            Type rootType,
            SerializedObject serializedObject);
    }
}
