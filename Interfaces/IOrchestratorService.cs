using GameStructures;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IOrchestratorService : IService
    {
        Task Initialize(int n);

        Task StartSimulation();

        Task StopSimulation();

        Task<Grid> GetCurrentGeneration();

        Task SendBlock(int index, Grid block);

        Task SyncWorkers(CancellationToken cancellationToken);

        Task<Borders> GetBorderCells(int index);
    }
}
