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

namespace Orchestrator
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class Orchestrator : StatelessService, IOrchestratorService
    {
        private const int barrierTimeout = 1000;

        private readonly int blockCount;
        private readonly Uri workerServiceUri;

        private readonly Barrier barrier;
        private readonly List<Grid> blocks;

        private Grid grid = null;

        public Orchestrator(StatelessServiceContext context)
            : base(context)
        {
            blockCount = int.Parse(Environment.GetEnvironmentVariable("BLOCK_COUNT"));

            var applicationName = context.CodePackageActivationContext.ApplicationName;
            workerServiceUri = new Uri($"{applicationName}/Worker");

            barrier = new Barrier(blockCount, (barrier) =>
            {
                if (!blocks.Contains(null))
                {
                    grid = Grid.MergeBlocks(blocks);
                }
            });

            blocks = new List<Grid>(blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                blocks.Add(null);
            }
        }

        public async Task Initialize(int n)
        {
            grid = null;
            var tasks = new List<Task>();

            for (int block = 0; block < blockCount; block++)
            {
                var serviceProxy = ServiceProxy.Create<IWorkerService>(
                    workerServiceUri,
                    new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(block));

                tasks.Add(serviceProxy.Initialize(block, blockCount == 1 ? n : n / 2));
            }

            await Task.WhenAll(tasks);
        }

        public async Task StartSimulation()
        {
            var tasks = new List<Task>();

            for (int block = 0; block < blockCount; block++)
            {
                var serviceProxy = ServiceProxy.Create<IWorkerService>(
                    workerServiceUri,
                    new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(block));

                tasks.Add(serviceProxy.StartSimulation());
            }

            await Task.WhenAll(tasks);
        }

        public async Task StopSimulation()
        {
            var tasks = new List<Task>();

            for (int block = 0; block < blockCount; block++)
            {
                var serviceProxy = ServiceProxy.Create<IWorkerService>(
                    workerServiceUri,
                    new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(block));

                tasks.Add(serviceProxy.StopSimulation());
            }

            await Task.WhenAll(tasks);
        }

        public async Task<Grid> GetCurrentGeneration()
        {
            if (grid == null)
            {
                List<Grid> blocks = new List<Grid>(blockCount);

                for (int block = 0; block < blockCount; block++)
                {
                    var serviceProxy = ServiceProxy.Create<IWorkerService>(
                        workerServiceUri,
                        new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(block));

                    blocks.Add(await serviceProxy.GetBlock());
                }

                grid = Grid.MergeBlocks(blocks);
            }

            return grid;
        }

        public async Task SendBlock(int index, Grid block)
        {
            await Task.Run(() => blocks[index] = block);
        }

        public async Task SyncWorkers(CancellationToken cancellationToken)
        {
            await Task.Run(() => barrier.SignalAndWait(barrierTimeout, cancellationToken));
        }

        public async Task<Borders> GetBorderCells(int index)
        {
            if (blocks.Contains(null)) return null;

            var blockNS = blocks[BlockNS(index)];
            var blockWE = blocks[BlockWE(index)];
            var blockCorner = blocks[BlockCorner(index)];

            var north = new List<Cell>();
            var south = new List<Cell>();

            for (int i = 0; i < blockNS.GetSize(); i++)
            {
                north.Add(blockNS[blockNS.GetSize() - 1, i]);
                south.Add(blockNS[0, i]);
            }

            var west = new List<Cell>();
            var east = new List<Cell>();

            for (int i = 0; i < blockWE.GetSize(); i++)
            {
                west.Add(blockWE[i, blockWE.GetSize() - 1]);
                east.Add(blockWE[i, 0]);
            }

            var corners = new List<Cell>();

            corners.Add(blockCorner[0, 0]);
            corners.Add(blockCorner[0, blockCorner.GetSize() - 1]);
            corners.Add(blockCorner[blockCorner.GetSize() - 1, 0]);
            corners.Add(blockCorner[blockCorner.GetSize() - 1, blockCorner.GetSize() - 1]);

            return await Task.FromResult(new Borders(north, south, west, east, corners));
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

        private int BlockNS(int index)
        {
            return (index + 2) % blockCount;
        }

        private int BlockWE(int index)
        {
            return index % 2 == 0 ? index + 1 : index - 1;
        }

        private int BlockCorner(int index)
        {
            return index switch
            {
                0 => 3,
                1 => 2,
                2 => 1,
                3 => 0,
                _ => throw new ArgumentException()
            };
        }
    }
}
