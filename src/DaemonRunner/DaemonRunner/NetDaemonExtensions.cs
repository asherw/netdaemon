using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetDaemon.Common;
using NetDaemon.Common.Reactive;
using NetDaemon.Configuration;
using NetDaemon.Daemon;
using NetDaemon.Daemon.Config;
using NetDaemon.Infrastructure.Extensions;
using NetDaemon.Service;
using NetDaemon.Service.App;
using NetDaemon.Service.Configuration;

namespace NetDaemon
{
    public static class NetDaemonExtensions
    {
        public static IHostBuilder UseNetDaemon(this IHostBuilder hostBuilder, ILogger logger)
        {
            // TODO: clean up.
            var sourceFolder = @"E:/Programming/netdaemon/netdaemon-app-template/";

            return hostBuilder.ConfigureServices((context, services) =>
            {
                // TODO: clean this up.
                var appCodeFolder = Path.Combine(sourceFolder, "apps");
                var appsAssembly = DaemonCompiler.GetCompiledAppsAssembly(appCodeFolder, logger);

                services.RegisterAllTypes<NetDaemonRxApp>(appsAssembly, ServiceLifetime.Singleton);
                services.RegisterAllTypes<NetDaemonApp>(appsAssembly, ServiceLifetime.Singleton);
                
                services.Configure<HomeAssistantSettings>(context.Configuration.GetSection("HomeAssistant"));
                services.Configure<NetDaemonSettings>(context.Configuration.GetSection("NetDaemon"));

                services.AddTransient<ILogger>(_ => logger);
                services.AddSingleton<IInstanceDaemonApp, CodeManager>();
                services.AddSingleton<YamlConfig>();

                services.AddHttpClient();
                services.AddHostedService<RunnerService>();
            });
        }
    }
}