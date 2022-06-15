using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GameStructures
{
    [DataContract]
    public sealed class Borders
    {
        [DataMember]
        public readonly List<Cell> North;

        [DataMember]
        public readonly List<Cell> South;

        [DataMember]
        public readonly List<Cell> West;

        [DataMember]
        public readonly List<Cell> East;

        [DataMember]
        public readonly List<Cell> Corners;

        public Borders(
            List<Cell> north,
            List<Cell> south,
            List<Cell> west,
            List<Cell> east,
            List<Cell> corners)
        {
            North = north;
            South = south;
            West = west;
            East = east;
            Corners = corners;
        }
    }
}
