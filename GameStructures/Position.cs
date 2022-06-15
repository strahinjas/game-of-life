using System.Runtime.Serialization;

namespace GameStructures
{
    [DataContract]
    public sealed class Position
    {
        [DataMember]
        public readonly int X;

        [DataMember]
        public readonly int Y;

        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }
}
