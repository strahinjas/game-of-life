using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Interfaces;

namespace TimeTracker
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    internal class TimeTracker : Actor, ITimeTracker
    {
        private const string stopwatchName = "stopwatch";

        /// <summary>
        /// Initializes a new instance of TimeTracker
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public TimeTracker(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        public async Task<string> GetElapsedTime(CancellationToken cancellationToken)
        {
            var stopwatch = await StateManager.GetStateAsync<Stopwatch>(stopwatchName, cancellationToken);
            var timeSpan = stopwatch.Elapsed;

            return string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds / 10);
        }

        public async Task StartTimeTrack(CancellationToken cancellationToken)
        {
            var stopwatch = await StateManager.GetStateAsync<Stopwatch>(stopwatchName, cancellationToken);
            stopwatch.Start();
            await StateManager.AddOrUpdateStateAsync(stopwatchName, stopwatch, (key, value) => stopwatch, cancellationToken);
        }

        public async Task StopTimeTrack(CancellationToken cancellationToken)
        {
            var stopwatch = await StateManager.GetStateAsync<Stopwatch>(stopwatchName, cancellationToken);
            stopwatch.Stop();
            await StateManager.AddOrUpdateStateAsync(stopwatchName, stopwatch, (key, value) => stopwatch, cancellationToken);
        }

        public async Task Reset(CancellationToken cancellationToken)
        {
            var stopwatch = await StateManager.GetStateAsync<Stopwatch>(stopwatchName, cancellationToken);
            stopwatch.Reset();
            await StateManager.AddOrUpdateStateAsync(stopwatchName, stopwatch, (key, value) => stopwatch, cancellationToken);
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected async override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            // The StateManager is this actor's private state store.
            // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
            // Any serializable object can be saved in the StateManager.
            // For more information, see https://aka.ms/servicefabricactorsstateserialization
            if (!await StateManager.ContainsStateAsync(stopwatchName))
            {
                await StateManager.AddStateAsync(stopwatchName, new Stopwatch());
            }
        }

        protected override Task OnDeactivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor deactivated.");

            return StateManager.RemoveStateAsync(stopwatchName);
        }
    }
}
