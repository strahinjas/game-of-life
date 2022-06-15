using GameStructures;
using Interfaces;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace Stats
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class Stats : StatelessService, IStatsService
    {
        private readonly int blockCount;
        private readonly Uri workerServiceUri;

        public Stats(StatelessServiceContext context)
            : base(context)
        {
            blockCount = int.Parse(Environment.GetEnvironmentVariable("BLOCK_COUNT"));

            var applicationName = context.CodePackageActivationContext.ApplicationName;
            workerServiceUri = new Uri($"{applicationName}/Worker");
        }

        public async Task<GameStats> GetGameStats()
        {
            var gameStats = new GameStats();

            for (int block = 0; block < blockCount; block++)
            {
                var serviceProxy = ServiceProxy.Create<IWorkerService>(
                    workerServiceUri,
                    new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(block));

                gameStats += await serviceProxy.GetPartialGameStats();
            }

            return gameStats;
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
