using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JoySoftware.HomeAssistant.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.Configuration;
using NetDaemon.Daemon;
using NetDaemon.Daemon.Storage;
using NetDaemon.Service.App;
using NetDaemon.Service.Configuration;

namespace NetDaemon.Service
{
    public class RunnerService : BackgroundService
    {
        /// <summary>
        /// The interval used when disconnected
        /// </summary>
        private const int ReconnectInterval = 40000;
        private const string Version = "dev";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IInstanceDaemonApp _codeManager;
        private readonly HomeAssistantSettings _homeAssistantSettings;
        private readonly NetDaemonSettings _netDaemonSettings;

        private readonly ILogger<RunnerService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private bool _entitiesGenerated;

        public RunnerService(
            ILoggerFactory loggerFactory, 
            IHttpClientFactory httpClientFactory, 
            IOptions<NetDaemonSettings> netDaemonSettings,
            IOptions<HomeAssistantSettings> homeAssistantSettings,
            IInstanceDaemonApp codeManager
            )
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RunnerService>();
            _httpClientFactory = httpClientFactory;
            _codeManager = codeManager;
            _homeAssistantSettings = homeAssistantSettings.Value;
            _netDaemonSettings = netDaemonSettings.Value;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping NetDaemon...");
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (_netDaemonSettings == null)
                {
                    _logger.LogError("No config specified, file or environment variables! Exiting...");
                    return;
                }

                EnsureApplicationDirectoryExists(_netDaemonSettings);

                var storageFolder = Path.Combine(_netDaemonSettings.SourceFolder!, ".storage");
                var sourceFolder = Path.Combine(_netDaemonSettings.SourceFolder!, "apps");

                var hasConnectedBefore = false;

                CollectibleAssemblyLoadContext? alc = null;

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (hasConnectedBefore)
                        {
                            // This is due to re-connect, it must be a re-connect
                            // so delay before retry connect again
                            await Task.Delay(ReconnectInterval, stoppingToken).ConfigureAwait(false); // Wait x seconds
                            _logger.LogInformation($"Restarting NeDaemon (version {Version})...");
                        }

                        await using var daemonHost =
                            new NetDaemonHost(
                                new HassClient(_loggerFactory),
                                new DataRepository(storageFolder),
                                _loggerFactory,
                                new HttpHandler(_httpClientFactory)
                            );
                        {

                            var daemonHostTask = daemonHost.Run(
                                _homeAssistantSettings.Host,
                                _homeAssistantSettings.Port,
                                _homeAssistantSettings.Ssl,
                                _homeAssistantSettings.Token,
                                stoppingToken
                            );

                            await WaitForDaemonToConnect(daemonHost, stoppingToken).ConfigureAwait(false);

                            if (!stoppingToken.IsCancellationRequested)
                            {
                                if (daemonHost.Connected)
                                {
                                    try
                                    {
                                        // Generate code if requested
                                        await GenerateEntities(daemonHost, sourceFolder);

                                        await daemonHost.Initialize(_codeManager).ConfigureAwait(false);

                                        // Wait until daemon stops
                                        await daemonHostTask.ConfigureAwait(false);
                                        if (!stoppingToken.IsCancellationRequested)
                                        {
                                            // It is disconnected, wait
                                            _logger.LogWarning($"Home assistant is unavailable, retrying in {ReconnectInterval / 1000} seconds...");
                                        }
                                    }
                                    catch (TaskCanceledException)
                                    {
                                        _logger.LogInformation("Canceling NetDaemon service...");
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogError(e, "Failed to load applications");
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"Home Assistant Core still unavailable, retrying in {ReconnectInterval / 1000} seconds...");
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (!stoppingToken.IsCancellationRequested)
                        {
                            _logger.LogWarning($"Home assistant is disconnected, retrying in {ReconnectInterval / 1000} seconds...");
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "MAJOR ERROR!");
                    }

                    // If we reached here it could be a re-connect
                    hasConnectedBefore = true;

                }
                if (alc is object)
                {
                    //loadedDaemonApps = null;
                    var alcWeakRef = new WeakReference(alc, trackResurrection: true);
                    alc.Unload();
                    alc = null;

                    for (int i = 0; alcWeakRef.IsAlive && (i < 100); i++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            } // Normal exit
            catch (Exception e)
            {
                _logger.LogError(e, "NetDaemon had unhandled exception, closing...");
            }

            _logger.LogInformation("NetDaemon exited!");
        }

        private async Task GenerateEntities(NetDaemonHost daemonHost, string sourceFolder)
        {
            if (!_netDaemonSettings.GenerateEntities.GetValueOrDefault())
                return;

            if (_entitiesGenerated)
                return;

            _entitiesGenerated = true;
            var codeGen = new CodeGenerator();
            var source = codeGen.GenerateCode(
                "Netdaemon.Generated.Extensions",
                daemonHost.State.Select(n => n.EntityId).Distinct()
            );

            await File.WriteAllTextAsync(Path.Combine(sourceFolder!, "_EntityExtensions.cs"), source).ConfigureAwait(false);

            var services = await daemonHost.GetAllServices();
            var sourceRx = codeGen.GenerateCodeRx(
                "Netdaemon.Generated.Reactive",
                daemonHost.State.Select(n => n.EntityId).Distinct(),
                services
            );

            await File.WriteAllTextAsync(Path.Combine(sourceFolder!, "_EntityExtensionsRx.cs"), sourceRx).ConfigureAwait(false);
        }

        private void EnsureApplicationDirectoryExists(NetDaemonSettings settings)
        {
            settings.SourceFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".netdaemon");
            var appDirectory = Path.Combine(settings.SourceFolder, "apps");

            Directory.CreateDirectory(appDirectory);
        }

        private async Task WaitForDaemonToConnect(NetDaemonHost daemonHost, CancellationToken stoppingToken)
        {
            var nrOfTimesCheckForConnectedState = 0;

            while (!daemonHost.Connected && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
                if (nrOfTimesCheckForConnectedState++ > 5)
                    break;
            }
        }
    }
}