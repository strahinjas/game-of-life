using System.Runtime.Serialization;

namespace GameStructures
{
    [DataContract]
    public sealed class Cell
    {
        [DataMember]
        public readonly bool IsAlive;

        public Cell(bool isAlive = false) => IsAlive = isAlive;

        public char ToChar() => IsAlive ? 'O' : '.';

        public override string ToString() => IsAlive ? "Alive" : "Dead";
    }
}
