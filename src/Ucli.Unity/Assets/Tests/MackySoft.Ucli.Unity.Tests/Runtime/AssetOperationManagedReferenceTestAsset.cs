using System;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class AssetOperationManagedReferenceTestAsset : ScriptableObject
    {
        [SerializeReference]
        private NodeBase? node = new IntegerNode(7);

        public NodeBase? Node => node;

        public void SetNode (NodeBase? value)
        {
            node = value;
        }

        [Serializable]
        public abstract class NodeBase
        {
        }

        [Serializable]
        public sealed class IntegerNode : NodeBase
        {
            [SerializeField]
            private int number;

            public IntegerNode ()
            {
            }

            public IntegerNode (int number)
            {
                this.number = number;
            }

            public int Number => number;
        }

        [Serializable]
        public sealed class TextNode : NodeBase
        {
            [SerializeField]
            private string text = string.Empty;

            public TextNode ()
            {
            }

            public TextNode (string text)
            {
                this.text = text;
            }

            public string Text => text;
        }
    }
}
