using GameStructures;
using Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Interfaces;

namespace GameWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private const int minN = 4;
        private const int maxN = 1024;

        private const string timeTrackerActorId = "timeTracker";

        private readonly Uri orchestratorServiceUri;
        private readonly Uri statsServiceUri;
        private readonly Uri timeTrackerActorServiceUri;

        public GameController(StatelessServiceContext context)
        {
            orchestratorServiceUri = GameWeb.GetOrchestratorServiceUri(context);
            statsServiceUri = GameWeb.GetStatsServiceUri(context);
            timeTrackerActorServiceUri = GameWeb.GetTimeTrackerActorServiceUri(context);
        }

        [HttpPost]
        [Route("init")]
        public async Task<IActionResult> Initialize([FromQuery] int n)
        {
            GameWeb.RegisterRequestForMetrics();

            if (n < minN || n > maxN)
            {
                return BadRequest();
            }

            await GetOrchestratorService().Initialize(n);
            await GetTimeTrackerActorService().Reset(new CancellationToken());

            return Ok();
        }

        [HttpPost]
        [Route("start")]
        public async Task StartSimulation()
        {
            await GetOrchestratorService().StartSimulation();
            await GetTimeTrackerActorService().StartTimeTrack(new CancellationToken());
        }

        [HttpPost]
        [Route("stop")]
        public async Task StopSimulation()
        {
            GameWeb.RegisterRequestForMetrics();

            await GetOrchestratorService().StopSimulation();
            await GetTimeTrackerActorService().StopTimeTrack(new CancellationToken());
        }

        [HttpGet]
        [Route("grid")]
        public async Task<IActionResult> GetGrid()
        {
            GameWeb.RegisterRequestForMetrics();

            var grid = await GetOrchestratorService().GetCurrentGeneration();

            return new JsonResult(grid.ToBoolGrid());
        }

        [HttpGet]
        [Route("char-grid")]
        public async Task<IActionResult> GetCharGrid()
        {
            GameWeb.RegisterRequestForMetrics();

            var grid = await GetOrchestratorService().GetCurrentGeneration();

            return new JsonResult(grid.ToCharGrid());
        }

        [HttpGet]
        [Route("time")]
        public async Task<string> GetSimulationTime()
        {
            GameWeb.RegisterRequestForMetrics();

            return await GetTimeTrackerActorService().GetElapsedTime(new CancellationToken());
        }

        [HttpGet]
        [Route("stats")]
        public async Task<GameStats> GetGameStats()
        {
            GameWeb.RegisterRequestForMetrics();

            return await GetStatsService().GetGameStats();
        }

        private IOrchestratorService GetOrchestratorService()
            => ServiceProxy.Create<IOrchestratorService>(orchestratorServiceUri);

        private IStatsService GetStatsService()
            => ServiceProxy.Create<IStatsService>(statsServiceUri);

        private ITimeTracker GetTimeTrackerActorService()
            => ActorProxy.Create<ITimeTracker>(new ActorId(timeTrackerActorId), timeTrackerActorServiceUri);
    }
}
