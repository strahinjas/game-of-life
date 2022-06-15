using GameStructures;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IStatsService : IService
    {
        Task<GameStats> GetGameStats();
    }
}
