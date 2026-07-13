using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class IndexCatalogTestComponent : MonoBehaviour
    {
        [SerializeField]
        private int integerValue;

        [SerializeField]
        private List<IndexCatalogNestedValue> items = new();

        [SerializeReference]
        private IndexCatalogTestSerializeReferenceCandidate? referenceValue;

        [Serializable]
        public sealed class IndexCatalogNestedValue
        {
            [SerializeField]
            private string value = string.Empty;
        }
    }
}
