using GameStructures;
using Interfaces;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace Worker
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class Worker : StatefulService, IWorkerService
    {
        private const string generationsDictionaryName = "generations";
        private const string statsDictionaryName = "stats";

        private const string simulationStartedKey = "simulationStarted";
        private const string indexKey = "index";
        private const string partitionKey = "partition";
        private const string aliveCountKey = "aliveCount";
        private const string deadCountKey = "deadCount";

        private const int cooldownTimeout = 1000;

        private readonly Random random = new Random();

        private readonly Uri orchestratorServiceUri;

        public Worker(StatefulServiceContext context)
            : base(context)
        {
            var applicationName = context.CodePackageActivationContext.ApplicationName;
            orchestratorServiceUri = new Uri($"{applicationName}/Orchestrator");
        }

        public async Task Initialize(int index, int n)
        {
            await ResetData(index);
            await InitializeBlock(n);
        }

        public async Task<GameStats> GetPartialGameStats()
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            var aliveCount = await statsDictionary.TryGetValueAsync(tx, aliveCountKey);
            var deadCount = await statsDictionary.TryGetValueAsync(tx, deadCountKey);

            return new GameStats(aliveCount.Value, deadCount.Value);
        }

        public async Task<Grid> GetBlock()
        {
            var generationsDictionary = await GetGenerationsDictionary();

            using var tx = StateManager.CreateTransaction();
            var block = await generationsDictionary.TryGetValueAsync(tx, await CurrentGeneration());

            return block.Value;
        }

        public async Task StartSimulation()
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            await statsDictionary.TryUpdateAsync(tx, simulationStartedKey, 1, 0);

            await tx.CommitAsync();
        }

        public async Task StopSimulation()
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            await statsDictionary.TryUpdateAsync(tx, simulationStartedKey, 0, 1);

            await tx.CommitAsync();
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var statsDictionary = await GetStatsDictionary();

                using var tx = StateManager.CreateTransaction();
                var simulationStarted = await statsDictionary.TryGetValueAsync(tx, simulationStartedKey);

                if (simulationStarted.HasValue && simulationStarted.Value == 1)
                {
                    var partition = await GetPartitionKey();
                    var orchestrator = GetOrchestratorService();

                    await orchestrator.SendBlock(partition, await GetBlock());

                    await orchestrator.SyncWorkers(cancellationToken);

                    var borders = await orchestrator.GetBorderCells(partition);
                    if (borders == null) continue;

                    await CalculateNextGeneration(borders);
                    await SwapGenerations();

                    //  await orchestrator.SyncWorkers(cancellationToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(cooldownTimeout), cancellationToken);
                }
            }
        }

        private IOrchestratorService GetOrchestratorService()
            => ServiceProxy.Create<IOrchestratorService>(orchestratorServiceUri);

        private async Task<IReliableDictionary<string, int>> GetStatsDictionary()
        {
            return await StateManager.GetOrAddAsync<IReliableDictionary<string, int>>(statsDictionaryName);
        }

        private async Task<IReliableDictionary<int, Grid>> GetGenerationsDictionary()
        {
            return await StateManager.GetOrAddAsync<IReliableDictionary<int, Grid>>(generationsDictionaryName);
        }

        private async Task ResetData(int partition)
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            await statsDictionary.AddOrUpdateAsync(tx, simulationStartedKey, 0, (key, value) => 0);
            await statsDictionary.AddOrUpdateAsync(tx, indexKey, 0, (key, value) => 0);
            await statsDictionary.AddOrUpdateAsync(tx, partitionKey, partition, (key, value) => partition);
            await statsDictionary.AddOrUpdateAsync(tx, aliveCountKey, 0, (key, value) => 0);
            await statsDictionary.AddOrUpdateAsync(tx, deadCountKey, 0, (key, value) => 0);

            await tx.CommitAsync();
        }

        private async Task UpdateStats(GameStats gameStats)
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            await statsDictionary.AddOrUpdateAsync(tx, aliveCountKey, gameStats.AliveCount, (key, value) => gameStats.AliveCount);
            await statsDictionary.AddOrUpdateAsync(tx, deadCountKey, gameStats.DeadCount, (key, value) => gameStats.DeadCount);

            await tx.CommitAsync();
        }

        private async Task<int> GetPartitionKey()
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            var partition = await statsDictionary.TryGetValueAsync(tx, partitionKey);

            return partition.Value;
        }

        private async Task<int> CurrentGeneration()
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            var index = await statsDictionary.TryGetValueAsync(tx, indexKey);

            return index.Value;
        }

        private async Task<int> NextGeneration()
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            var index = await statsDictionary.TryGetValueAsync(tx, indexKey);

            return (index.Value + 1) % 2;
        }

        private async Task SwapGenerations()
        {
            var statsDictionary = await GetStatsDictionary();

            using var tx = StateManager.CreateTransaction();
            await statsDictionary.TryUpdateAsync(tx, indexKey, await NextGeneration(), await CurrentGeneration());
            await tx.CommitAsync();
        }

        private async Task InitializeBlock(int n)
        {
            var generationsDictionary = await GetGenerationsDictionary();
            await generationsDictionary.ClearAsync();

            var block = new Grid(n);

            using var tx = StateManager.CreateTransaction();
            await generationsDictionary.AddAsync(tx, await NextGeneration(), block);

            var gameStats = new GameStats();

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    var cell = new Cell(random.NextDouble() >= 0.5);

                    if (cell.IsAlive) gameStats.AliveCount++;
                    else gameStats.DeadCount++;

                    block[x, y] = cell;
                }
            }
            await generationsDictionary.AddAsync(tx, await CurrentGeneration(), block);
            await tx.CommitAsync();

            await UpdateStats(gameStats);
        }

        private async Task CalculateNextGeneration(Borders borders)
        {
            var generationsDictionary = await GetGenerationsDictionary();

            using var tx = StateManager.CreateTransaction();
            var current = await generationsDictionary.TryGetValueAsync(tx, await CurrentGeneration());
            var next = await generationsDictionary.TryGetValueAsync(tx, await NextGeneration());

            var n = current.Value.GetSize();
            var gameStats = new GameStats();

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    var cell = current.Value[x, y];
                    var neighborsAlive = GetAliveNeighborsCount(x, y, current.Value, borders);

                    var isCellAlive = (cell.IsAlive && neighborsAlive == 2) || neighborsAlive == 3;

                    if (isCellAlive) gameStats.AliveCount++;
                    else gameStats.DeadCount++;

                    next.Value[x, y] = new Cell(isCellAlive);
                }
            }

            await generationsDictionary.AddOrUpdateAsync(tx, await NextGeneration(), next.Value, (k, v) => next.Value);
            await tx.CommitAsync();

            await UpdateStats(gameStats);
        }

        private int GetAliveNeighborsCount(int x, int y, Grid grid, Borders borders)
        {
            var count = 0;
            var n = grid.GetSize();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        Cell neighbor;
                        int nx = x + dx;
                        int ny = y + dy;

                        // Corners
                        if (nx < 0 && ny < 0) neighbor = borders.Corners[3];
                        else if (nx < 0 && ny > n - 1) neighbor = borders.Corners[2];
                        else if (nx > n - 1 && ny < 0) neighbor = borders.Corners[1];
                        else if (nx > n - 1 && ny > n - 1) neighbor = borders.Corners[0];

                        // Borders
                        else if (nx < 0) neighbor = borders.South[ny];
                        else if (nx > n - 1) neighbor = borders.North[ny];
                        else if (ny < 0) neighbor = borders.East[nx];
                        else if (ny > n - 1) neighbor = borders.West[nx];

                        // Inside same partition
                        else neighbor = grid[nx, ny];

                        if (neighbor != null && neighbor.IsAlive) count++;
                    }
                }
            }

            return count;
        }
    }
}
