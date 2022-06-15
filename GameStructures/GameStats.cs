using System.Runtime.Serialization;

namespace GameStructures
{
    [DataContract]
    public class GameStats
    {
        [DataMember]
        public int AliveCount { get; set; }

        [DataMember]
        public int DeadCount { get; set; }

        public GameStats(int aliveCount = 0, int deadCount = 0)
        {
            AliveCount = aliveCount;
            DeadCount = deadCount;
        }

        public static GameStats operator +(GameStats left, GameStats right)
            => new GameStats(left.AliveCount + right.AliveCount, left.DeadCount + right.DeadCount);
    }
}
