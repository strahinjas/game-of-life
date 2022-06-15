using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading;
using System.Threading.Tasks;

[assembly: FabricTransportActorRemotingProvider(RemotingListenerVersion = RemotingListenerVersion.V2_1, RemotingClientVersion = RemotingClientVersion.V2_1)]
namespace TimeTracker.Interfaces
{
    /// <summary>
    /// This interface defines the methods exposed by an actor.
    /// Clients use this interface to interact with the actor that implements it.
    /// </summary>
    public interface ITimeTracker : IActor
    {
        Task<string> GetElapsedTime(CancellationToken cancellationToken);

        Task StartTimeTrack(CancellationToken cancellationToken);

        Task StopTimeTrack(CancellationToken cancellationToken);

        Task Reset(CancellationToken cancellationToken);
    }
}
