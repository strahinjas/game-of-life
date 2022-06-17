using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameWeb
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class GameWeb : StatelessService
    {
        private static readonly object metricsLock = new object();

        private static readonly string totalRequestsPerSecondMetricName = "TotalRequestsPerSecond";
        private static readonly TimeSpan metricsReportingInterval = TimeSpan.FromSeconds(1);
        private static int requestCountLastSecond = 0;

        private static readonly TimeSpan scaleInterval = TimeSpan.FromMinutes(1);

        public GameWeb(StatelessServiceContext context)
            : base(context)
        { }

        public static void RegisterRequestForMetrics()
        {
            lock (metricsLock)
            {
                requestCountLastSecond++;
            }
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatelessServiceContext>(serviceContext))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await DefineMetricsAndPolicies();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (metricsLock)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, $"GameWeb reports {requestCountLastSecond} requests.");
                    Partition.ReportLoad(new List<LoadMetric> { new LoadMetric(totalRequestsPerSecondMetricName, requestCountLastSecond) });
                    requestCountLastSecond = 0;
                }

                await Task.Delay(metricsReportingInterval, cancellationToken);
            }
        }

        private async Task DefineMetricsAndPolicies()
        {
            FabricClient fabricClient = new FabricClient();
            StatelessServiceUpdateDescription updateDescription = new StatelessServiceUpdateDescription();

            RegisterMetrics(updateDescription);
            RegisterScaling(updateDescription);

            await fabricClient.ServiceManager.UpdateServiceAsync(Context.ServiceName, updateDescription);
        }

        private void RegisterMetrics(StatelessServiceUpdateDescription updateDescription)
        {
            StatelessServiceLoadMetricDescription requestsPerSecondMetric = new StatelessServiceLoadMetricDescription
            {
                Name = totalRequestsPerSecondMetricName,
                DefaultLoad = 0,
                Weight = ServiceLoadMetricWeight.High
            };

            if (updateDescription.Metrics == null)
            {
                updateDescription.Metrics = new MetricsCollection();
            }
            updateDescription.Metrics.Add(requestsPerSecondMetric);
        }

        private void RegisterScaling(StatelessServiceUpdateDescription updateDescription)
        {
            PartitionInstanceCountScaleMechanism mechanism = new PartitionInstanceCountScaleMechanism
            {
                MaxInstanceCount = 5,
                MinInstanceCount = 1,
                ScaleIncrement = 1
            };

            AveragePartitionLoadScalingTrigger trigger = new AveragePartitionLoadScalingTrigger
            {
                MetricName = totalRequestsPerSecondMetricName,
                ScaleInterval = scaleInterval,
                LowerLoadThreshold = 15.0,
                UpperLoadThreshold = 45.0
            };

            ScalingPolicyDescription policy = new ScalingPolicyDescription(mechanism, trigger);

            if (updateDescription.ScalingPolicies == null)
            {
                updateDescription.ScalingPolicies = new List<ScalingPolicyDescription>();
            }
            updateDescription.ScalingPolicies.Add(policy);
        }

        private static string GetApplicationName(ServiceContext context) => context.CodePackageActivationContext.ApplicationName;

        internal static Uri GetOrchestratorServiceUri(ServiceContext context) => new Uri($"{GetApplicationName(context)}/Orchestrator");

        internal static Uri GetStatsServiceUri(ServiceContext context) => new Uri($"{GetApplicationName(context)}/Stats");

        internal static Uri GetTimeTrackerActorServiceUri(ServiceContext context) => new Uri($"{GetApplicationName(context)}/TimeTrackerActorService");
    }
}
