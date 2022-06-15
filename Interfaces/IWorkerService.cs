using GameStructures;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IWorkerService : IService
    {
        Task Initialize(int index, int n);

        Task<GameStats> GetPartialGameStats();

        Task<Grid> GetBlock();

        Task StartSimulation();

        Task StopSimulation();
    }
}
